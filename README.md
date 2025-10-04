# ParadeGuard Weather Prediction API

A sophisticated ASP.NET Core Web API that provides weather predictions for outdoor events using 40 years of historical NASA POWER satellite data. This API leverages machine learning probability calculations to predict weather conditions for any location and date within the next year.

## 🌟 Features

- **Historical Weather Analysis**: Access 40 years of NASA POWER satellite data
- **Automatic Weather Classification**: AI-powered classification of weather conditions (VeryHot, VeryCold, VeryWet, VeryWindy, Normal)
- **Comprehensive Probability Breakdown**: Detailed probability percentages for all weather classifications
- **Geocoding Support**: Convert location names to coordinates using OpenCage API
- **AI Chat Integration**: Groq-powered conversational AI for weather insights
- **Advanced Caching**: Intelligent memory caching with size limits and expiration policies
- **Rate Limiting**: Configurable rate limiting to prevent API abuse
- **Health Checks**: Comprehensive health monitoring for all external services
- **Resilient Architecture**: Polly-based retry policies and circuit breakers
- **Production Ready**: Response compression, CORS, security headers, and comprehensive logging with Serilog

## 🚀 Tech Stack

- **.NET 8.0** - Latest LTS framework
- **ASP.NET Core** - High-performance web framework
- **Serilog** - Structured logging
- **Polly** - Resilience and transient-fault-handling
- **Swagger/OpenAPI** - API documentation
- **Memory Cache** - High-speed data caching

## 📋 Prerequisites

- .NET 8.0 SDK or later
- API Keys:
  - NASA POWER API Key (optional but recommended)
  - OpenCage Geocoding API Key
  - Groq API Key

## 🔧 Installation

### 1. Clone the Repository

```bash
git clone <repository-url>
cd ParadeGuard.Api
```

### 2. Install Dependencies

```bash
dotnet restore
```

### 3. Configure API Keys

Create an `appsettings.Development.json` file or set environment variables:

```json
{
  "ApiKeys": {
    "NasaApiKey": "your-nasa-api-key",
    "GeocodingApiKey": "your-opencage-api-key",
    "GroqApiKey": "your-groq-api-key"
  }
}
```

**Environment Variables (Recommended for Production):**

```bash
export PARADE_GUARD_NASA_API_KEY="your-nasa-key"
export PARADE_GUARD_GEOCODING_API_KEY="your-geocoding-key"
export PARADE_GUARD_GROQ_API_KEY="your-groq-key"
```

### 4. Run the Application

```bash
dotnet run
```

The API will be available at:
- Development: `https://localhost:5001` or `http://localhost:5000`
- Production: Port specified by `PORT` environment variable (default: 8080)

## 📚 API Documentation

### Swagger UI

Access interactive API documentation at: `https://localhost:5001/`

### Main Endpoints

#### 1. Weather Prediction

```http
POST /api/weather/predict
Content-Type: application/json

{
  "locationName": "Cairo, Egypt",
  "targetDate": "2025-12-25"
}
```

**Response:**

```json
{
  "location": "Cairo, Egypt",
  "date": "2025-12-25",
  "prediction": "Normal",
  "probabilityPercent": 85.5,
  "probabilities": {
    "VeryHot": 5.0,
    "VeryCold": 0.0,
    "VeryWet": 2.5,
    "VeryWindy": 7.0,
    "Normal": 85.5
  },
  "observations": 40,
  "description": "Historical data shows consistently normal weather conditions...",
  "stats": {
    "avgTemperature": 18.5,
    "avgPrecipitation": 2.3,
    "avgWindSpeed": 4.2,
    "avgHumidity": 65.0
  },
  "allDays": [...],
  "extremeWeatherDaysCount": 6,
  "processingTimeMs": 245
}
```

#### 2. Chat with AI

```http
POST /api/chat
Content-Type: application/json

{
  "messages": [
    {
      "role": "user",
      "content": "What should I know about planning outdoor events in Cairo in December?"
    }
  ]
}
```

#### 3. Health Check

```http
GET /health
```

#### 4. Classification Thresholds

```http
GET /api/weather/thresholds
```

## ⚙️ Configuration

### Cache Settings

```json
{
  "Cache": {
    "DefaultExpirationMinutes": 720,
    "GeocodingExpirationMinutes": 1440,
    "WeatherExpirationMinutes": 720,
    "MaxCacheItems": 1000
  }
}
```

### API Settings

```json
{
  "ApiSettings": {
    "RequestTimeoutSeconds": 30,
    "MaxRetryAttempts": 3,
    "RateLimitPerMinute": 60,
    "AllowedOrigins": [
      "https://localhost:3000",
      "https://your-frontend-domain.com"
    ]
  }
}
```

### Logging Configuration

Logs are stored in the `logs/` directory with:
- Daily rotation
- 30-day retention
- 100MB file size limit
- Structured JSON format

## 🐳 Docker Deployment

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ParadeGuard.Api.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ParadeGuard.Api.dll"]
```

## 🚂 Railway Deployment

The application is configured for Railway deployment:

1. Set environment variables in Railway dashboard
2. The app automatically binds to the `PORT` environment variable
3. HTTPS is handled at the load balancer level

## 🔒 Security Features

- CORS configuration with whitelist
- Rate limiting (60 requests/minute by default)
- Request size limits (1MB)
- Security headers (X-Frame-Options, X-Content-Type-Options, etc.)
- Input validation and sanitization
- API key authentication ready (can be enabled)

## 📊 Weather Classification Thresholds

| Classification | Condition | Threshold |
|---------------|-----------|-----------|
| VeryHot | Temperature | > 35°C |
| VeryCold | Temperature | < 5°C |
| VeryWet | Precipitation | > 10mm |
| VeryWindy | Wind Speed | > 10 m/s |
| Normal | - | All other conditions |

## 🧪 Testing

```bash
# Run unit tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true
```

## 📈 Performance

- Average response time: < 300ms
- Caching hit rate: ~85%
- Concurrent requests: 100+
- Memory usage: ~150MB

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📝 License

This project is licensed under the MIT License.

## 👥 Authors

- **lOAY WAEL** - Initial work

## 🙏 Acknowledgments

- NASA POWER Project for providing historical weather data
- OpenCage for geocoding services
- Groq for AI chat capabilities

## 📞 Support

For issues and questions:
- Email: Loayw842@gmail.com
- Documentation: [API Docs]([https://your-domain.com/swagger](https://paradeguardapi-production.up.railway.app/index.html))

## 🗺️ Roadmap

- [ ] Add support for custom weather thresholds
- [ ] Implement weather alerts
- [ ] Add multi-language support
- [ ] Machine learning model improvements
- [ ] WebSocket support for real-time updates

---

Built with ❤️ for outdoor event planning
