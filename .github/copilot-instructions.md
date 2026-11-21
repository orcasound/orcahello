# GitHub Copilot Instructions for OrcaHello

## Project Overview

OrcaHello is a real-time AI-assisted killer whale notification system developed by [Orcasound](https://www.orcasound.net/). This system uses deep learning to detect orca whale calls from live hydrophone audio in Puget Sound, helping with the conservation of endangered Southern Resident Killer Whales (SRKW).

## Project Structure

This repository contains multiple interconnected components:

- **ModeratorFrontEnd**: Web applications for moderator portal (both AIForOrcas and OrcaHello implementations)
- **InferenceSystem**: Real-time AI inference engine for detecting whale calls
- **ModelTraining**: Machine learning model training and data preparation
- **ModelEvaluation**: Model benchmarking and evaluation tools
- **NotificationSystem**: Email notification system for confirmed detections

## Technology Stack

### Primary Technologies
- **.NET 6+**: Main framework for web applications and APIs
- **C#**: Primary programming language for backend services
- **Blazor**: Frontend framework for web UI components
- **Azure**: Cloud platform (CosmosDB,  Storage, App Services, Kubernetes)
- **Python**: ML/AI model training and inference
- **FastAPI/TensorFlow**: AI/ML frameworks

### Architecture Patterns
- Clean Architecture with separation of concerns
- Dependency injection and service-oriented design
- Repository pattern for data access
- API-first design with Swagger documentation

## Coding Standards

### General Guidelines
- Comments must end in punctuation (typically a period).
- **DON'T** include commented-out code. Remove dead code rather than commenting it out.
- All code functions should have function-level comments at the top of the function.

### C# Guidelines
- Use file-scoped namespaces (NET 6+ feature)
- Follow established naming conventions (PascalCase for public members, camelCase for private)
- Include XML documentation comments for public APIs
- Use `[ExcludeFromCodeCoverage]` attribute for configuration classes
- Implement proper error handling and logging
- Use async/await patterns for I/O operations

### Code Organization
- Organize code into logical layers (Controllers, Services, Brokers, Models)
- Use configuration classes for dependency injection
- Separate concerns between API and UI projects
- Include comprehensive unit tests for business logic

### Example Code Patterns

```csharp
// Controllers should be minimal and delegate to services
[ApiController]
[Route("api/[controller]")]
public class DetectionsController : ControllerBase
{
    private readonly IDetectionService _detectionService;
    
    public DetectionsController(IDetectionService detectionService)
    {
        _detectionService = detectionService;
    }
}

// Use configuration classes for dependency injection
[ExcludeFromCodeCoverage]
public static class WebServiceProviders
{
    public static void ConfigureServices(this WebApplicationBuilder builder)
    {
        // Configuration logic here
    }
}
```

## Domain-Specific Context

### Marine Biology & Acoustics
- **Hydrophones**: Underwater microphones that capture marine audio
- **Spectrograms**: Visual representations of audio frequency over time
- **Bioacoustics**: Scientific study of sound in living organisms
- **SRKW**: Southern Resident Killer Whales (endangered population)
- **Detection confidence**: Model probability scores for whale call classification

### Business Logic Context
- Detections require expert moderation before public notification
- Model processes 2-second audio segments with overlapping windows
- Confirmed SRKW detections trigger immediate notifications to subscribers
- System handles multiple hydrophone locations across Puget Sound
- Moderators can tag detections with various labels (species, behavior, etc.)

## Contributing Guidelines

### Before Contributing
1. Read [CONTRIBUTING.md](../CONTRIBUTING.md) and [CODE_OF_CONDUCT.md](../CODE_OF_CONDUCT.md)
2. Check existing issues and avoid duplicating work
3. Follow the established coding patterns and architecture
4. Include appropriate unit tests for new functionality
5. Update documentation for user-facing changes

### Pull Request Guidelines
- Reference GitHub issues in PR descriptions
- Ensure existing tests continue to pass
- Include tests for bug fixes and new features
- Maintain consistent coding style with existing codebase
- Run code formatting tools (Prettier for frontend, built-in .NET formatting)

### Code Reviews
- Minimum two reviewers for significant changes
- Domain experts review ML/AI components
- Infrastructure team reviews Azure and deployment changes
- Follow the CODEOWNERS file for appropriate reviewers

## Testing Approach

- Unit tests for business logic and services
- Integration tests for API endpoints
- Mock external dependencies (CosmosDB, Azure services)
- Test data fixtures should use realistic marine biology scenarios
- Performance testing for real-time inference requirements

## Security Considerations

- Authentication via Azure AD for moderator access
- Authorization policies for different user roles
- Secure handling of hydrophone audio data
- CORS policies configured for specific origins
- Environment-specific configuration management

## AI/ML Specific Guidelines

### Model Training
- Use versioned datasets with proper train/validation/test splits
- Document model performance metrics and evaluation criteria
- Include data preprocessing and feature engineering steps
- Version control trained models and track experiments

### Inference System
- Maintain low latency for real-time processing
- Handle model loading and initialization gracefully
- Include proper error handling for invalid audio data
- Monitor model performance and drift over time

## Environment and Deployment

- Development: Local development with mock services
- Staging: Azure-hosted environment for testing
- Production: Azure App Services with CosmosDB backend
- Use GitHub Actions for CI/CD workflows
- Follow infrastructure as code principles

## Common Patterns to Follow

1. **Configuration Management**: Use strongly-typed configuration classes
2. **Error Handling**: Implement consistent error responses and logging
3. **API Design**: Follow RESTful conventions with proper HTTP status codes
4. **Data Access**: Use repository pattern with async methods
5. **Authentication**: Leverage Azure AD integration for user management
6. **Monitoring**: Include appropriate logging and telemetry

## Resources

- [Project Wiki](https://github.com/orcasound/aifororcas-livesystem/wiki)
- [Public Moderator Portal](https://aifororcas.azurewebsites.net/)
- [Orcasound Website](https://www.orcasound.net/)
- [AI4Orcas Initiative](https://ai4orcas.net/)
