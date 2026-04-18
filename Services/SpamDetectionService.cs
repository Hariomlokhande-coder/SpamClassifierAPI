using Microsoft.Extensions.ML;
using Microsoft.ML;
using Microsoft.ML.Data;
using SpamClassifierAPI.Models;
using SpamClassifierAPI.Utils;
using System.Linq;

namespace SpamClassifierAPI.Services
{
    public class SpamDetectionService
    {
        private const string DATA_FILE = "spamdata.csv";
        private const string MODEL_FILE = "spam_model.zip";
        private const string DATA_FOLDER = "Data";

        private readonly MLContext _mlContext;
        private readonly Lazy<Task> _initializationTask;
        private readonly SemaphoreSlim _trainingLock = new SemaphoreSlim(1, 1);
        private readonly PredictionEnginePool<EmailData, EmailPrediction> _predictionEnginePool;
        private readonly ILogger<SpamDetectionService> _logger;

        public bool IsModelTrained { get; private set; } = false;

        public SpamDetectionService(PredictionEnginePool<EmailData, EmailPrediction> predictionEnginePool, ILogger<SpamDetectionService> logger)
        {
            _predictionEnginePool = predictionEnginePool;
            _logger = logger;
            _mlContext = new MLContext(seed: 1);

            //Lazy initialization
            _initializationTask = new Lazy<Task>(() => TrainModelAsync(false));
            _ = _initializationTask.Value;
        }

        //to insure model ready before prediction
    
        private async Task EnsureModelInitializedAsync()
        {
            await _initializationTask.Value.ConfigureAwait(false);
        }

        private string GetFileHash(string filePath)
        {
            if (!File.Exists(filePath)) return string.Empty;
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        //Load data and clean
        private async Task<IDataView> LoadAndCleanDataAsync(string path)
        {
            var rawData = await Task.Run(() =>
                _mlContext.Data.LoadFromTextFile<EmailData>(
                    path: path,
                    hasHeader: true,
                    separatorChar: ','));

            var cleanedList = _mlContext.Data
                .CreateEnumerable<EmailData>(rawData, false)
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Message))
                .Select(x => new EmailData
                {
                    Label = x.Label,
                    Message = TextPreprocessor.Clean(x.Message ?? string.Empty)
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Message))
                .ToList();

            if (!cleanedList.Any())
                throw new InvalidOperationException("All data became empty after cleaning");

            return _mlContext.Data.LoadFromEnumerable(cleanedList);
        }

        //build pipeline and train model
        private async Task BuildAndFitPipelineAsync(IDataView data)
        {
            var split = _mlContext.Data.TrainTestSplit(data, testFraction: 0.2);

            var pipeline = _mlContext.Transforms.Text.FeaturizeText(
                                "Features",
                                nameof(EmailData.Message))
                           .Append(_mlContext.BinaryClassification.Trainers.SdcaLogisticRegression());

            await Task.Run(async () =>
            {
                var trainedModel = pipeline.Fit(split.TrainSet);

                if (trainedModel == null)
                    throw new Exception("Model training failed");

                var predictions = trainedModel.Transform(split.TestSet);
                
                try
                {
                    var metrics = _mlContext.BinaryClassification.Evaluate(predictions);

                    _logger.LogInformation("Model metrics — Accuracy: {Accuracy:P2}, F1: {F1:F4}, AUC: {AUC:F4}",
                        metrics.Accuracy, metrics.F1Score, metrics.AreaUnderRocCurve);
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    _logger.LogWarning(ex, "Evaluation skipped — test set lacks one class (positive/negative).");
                }
                // Save the model with a retry mechanism to handle file locks
                if (trainedModel == null)
                    throw new InvalidOperationException("Model cannot be null before saving.");

                string modelPath = Path.Combine(Directory.GetCurrentDirectory(), DATA_FOLDER, MODEL_FILE);
                string? directory = Path.GetDirectoryName(modelPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                int retries = 3;
                 while (retries > 0)
                 {
                     try
                     {
                         using (var stream = new FileStream(modelPath, FileMode.Create, FileAccess.Write, FileShare.None))
                         {
                             _mlContext.Model.Save(trainedModel, data.Schema, stream);
                         }
                         _logger.LogInformation("Model saved to: {ModelPath}", modelPath);
                         break;
                     }
                    catch (IOException ex)
                    {
                        retries--;
                        if (retries == 0)
                        {
                            throw new IOException($"Failed to save model to {modelPath} after multiple attempts.", ex);
                        }
                        _logger.LogWarning("Model file locked, retrying save... ({RetriesLeft} retries left)", retries);
                        await Task.Delay(500); // Wait 500ms before retrying
                    }
                }
            });
        }

        //Train model
        public async Task TrainModelAsync(bool forceRetrain = false)
        {
            await _trainingLock.WaitAsync();
            try
            {
                string dataPath = Path.Combine(Directory.GetCurrentDirectory(), DATA_FOLDER, DATA_FILE);
                string modelPath = Path.Combine(Directory.GetCurrentDirectory(), DATA_FOLDER, MODEL_FILE);
                string hashPath = Path.Combine(Directory.GetCurrentDirectory(), DATA_FOLDER, "spamdata_hash.txt");

                if (!File.Exists(dataPath))
                    throw new FileNotFoundException("Dataset not found");

                string currentDataHash = GetFileHash(dataPath);
                string savedDataHash = File.Exists(hashPath) ? await File.ReadAllTextAsync(hashPath) : string.Empty;

                bool dataChanged = currentDataHash != savedDataHash;

                // Check if we can skip training (data hasn't changed AND model exists)
                if (!forceRetrain && !dataChanged && File.Exists(modelPath))
                {
                    if (!IsModelTrained)
                    {
                        IsModelTrained = true;
                        _logger.LogInformation("Existing model on disk is up-to-date. Skipping retrain.");
                    }
                    else
                    {
                        _logger.LogDebug("Data unchanged since last train. Skipping retrain.");
                    }
                    return;
                }

                // If we reach here, we must train
                _logger.LogInformation(dataChanged ? "Data change detected. Starting training..." : "Force retrain triggered.");

                var data = await LoadAndCleanDataAsync(dataPath);

                var count = _mlContext.Data.CreateEnumerable<EmailData>(data, false).Count();
                if (count < 5)
                    throw new InvalidOperationException("Not enough data to train model");

                await BuildAndFitPipelineAsync(data);

                // Update hash trace so next time we know we already trained on this exact file
                await File.WriteAllTextAsync(hashPath, currentDataHash);

                IsModelTrained = true;
            }
            finally
            {
                _trainingLock.Release();
            }
        }
        //Predict
        
        public async Task<EmailPrediction> PredictAsync(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Message cannot be empty");

            await EnsureModelInitializedAsync();

            if (input.Length > 5000)
            {
                // Truncate instead of rejecting large queries
                input = input.Substring(0, 5000);
            }

            var cleaned = TextPreprocessor.Clean(input);

            if (string.IsNullOrWhiteSpace(cleaned))
            {
                // Graceful fallback for strings that lose all content after cleaning (e.g., stopwords only)
                return new EmailPrediction { Prediction = false, Probability = 0.5f, Score = 0f };
            }

            int retryCount = 10;
            while (retryCount > 0)
            {
                try
                {
                    return _predictionEnginePool.Predict(modelName: "SpamModel", new EmailData
                    {
                        Message = cleaned
                    });
                }
                catch (InvalidOperationException) when (retryCount > 1)
                {
                    // Handles the PredictionEnginePool watchForChanges physical file ingestion delay
                    retryCount--;
                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Prediction failed due to an error: {ex.Message}", ex);
                }
            }

            throw new InvalidOperationException("Prediction failed: Cannot initialize underlying model pool in time.");
        }
    }
}