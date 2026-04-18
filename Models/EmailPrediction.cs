using Microsoft.ML.Data;
namespace SpamClassifierAPI.Models
{
    public class EmailPrediction
    {
        [ColumnName("PredictedLabel")]
        public bool Prediction { get; set; }

        //Confidence score(0 to 1)
        public float Probability { get; set; }
        //Raw score from the model that we get
        public float Score { get; set; }
    }
}
