# SpamClassifierAPI

An ASP.NET Core (`net8.0`) web application and API for spam message classification using ML.NET.  
The project includes:
- JWT based authentication
- ML model training and prediction endpoints
- SQL Server (LocalDB) persistence for users
- Swagger UI for API testing
- Simple MVC pages for login/signup and home

## Tech Stack

- ASP.NET Core 8
- Entity Framework Core (SQL Server)
- ML.NET + `PredictionEnginePool`
- JWT Bearer Authentication
- Serilog
- Swagger (Swashbuckle)

## Project Structure

- `Controllers/` - API and MVC controllers (`AuthController`, `SpamController`, `HomeController`)
- `Services/SpamDetectionService.cs` - training + prediction logic
- `Data/spamdata.csv` - training dataset
- `Data/spam_model.zip` - generated model file (created after training)
- `Models/` - request/response and data models
- `Program.cs` - service registration and middleware pipeline

## Prerequisites

- .NET 8 SDK installed
- SQL Server LocalDB available

## Getting Started

1. Restore packages:

```bash
dotnet restore
```

2. Apply database migrations:

```bash
dotnet ef database update
```

3. Run the application:

```bash
dotnet run
```

4. Open Swagger:

`http://localhost:<port>/swagger`

> Note: Non-HTTPS root requests are redirected to Swagger by middleware.

## Authentication Flow

Most spam endpoints are protected with `[Authorize]`.

1. Create account:
   - `POST /api/auth/signup`
2. Login:
   - `POST /api/auth/login`
   - Response contains JWT token
3. In Swagger, click **Authorize** and use:
   - `Bearer <your_token>`

## API Endpoints

### Auth

- `POST /api/auth/signup`
- `POST /api/auth/login`

### Spam

- `POST /api/spam/train?forceRetrain=false`
  - Trains model only when data has changed (unless forced)
- `POST /api/spam/predict`
  - Request body:

```json
{
  "message": "Congratulations! You won a free prize. Click now!"
}
```

## Model Training Notes

- Training data is loaded from `Data/spamdata.csv`.
- Dataset hash is stored in `Data/spamdata_hash.txt`.
- If data hash is unchanged and model exists, retraining is skipped.
- Model is saved to `Data/spam_model.zip`.

## Logging

Serilog is configured to write logs to:
- Console
- `Logs/spam-classifier-.log` (daily rolling, 14 files retained)

## Default Configuration

Defined in `appsettings.json`:
- LocalDB connection string
- JWT key/issuer/audience/expiry

For production:
- move secrets (JWT key, connection string) to environment variables or secure secret store.

## License

Add your preferred license information here.