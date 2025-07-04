# Contributing to Immich Downloader

Thank you for your interest in contributing to Immich Downloader! This document provides guidelines and information for contributors.

## 🎯 How to Contribute

### Reporting Bugs

1. **Check existing issues** to avoid duplicates
2. **Use the bug report template** when creating new issues
3. **Include system information**: OS, .NET version, browser version
4. **Provide steps to reproduce** the issue
5. **Include logs and screenshots** when relevant

### Suggesting Features

1. **Check the roadmap** to see if the feature is already planned
2. **Use the feature request template** for new suggestions
3. **Explain the use case** and benefit to users
4. **Consider implementation complexity** and alternatives

### Code Contributions

1. **Fork the repository** and create a feature branch
2. **Follow coding standards** outlined below
3. **Add tests** for new functionality
4. **Update documentation** as needed
5. **Submit a pull request** with clear description

## 🛠️ Development Setup

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 18+](https://nodejs.org/)
- [Git](https://git-scm.com/)
- [Docker](https://www.docker.com/) (optional)

### Local Development

1. **Clone your fork**:
   ```bash
   git clone https://github.com/your-username/immich-downloader.git
   cd immich-downloader
   ```

2. **Setup backend**:
   ```bash
   cd backend
   dotnet restore
   dotnet ef database update --project ImmichDownloader.Web
   ```

3. **Setup frontend**:
   ```bash
   cd frontend
   npm install
   ```

4. **Configure environment**:
   ```bash
   cp .env.example .env
   # Edit .env with your Immich server details
   ```

5. **Start development servers**:
   ```bash
   # Terminal 1 - Backend
   cd backend
   dotnet run --project ImmichDownloader.Web

   # Terminal 2 - Frontend
   cd frontend
   npm start
   ```

## 📋 Coding Standards

### C# Backend

#### File Organization
- **One class per file** with matching filename
- **Use file-scoped namespaces**:
  ```csharp
  namespace ImmichDownloader.Web.Services;

  public class MyService
  {
      // implementation
  }
  ```

#### Documentation
- **XML documentation** for all public members:
  ```csharp
  /// <summary>
  /// Downloads an album from the Immich server.
  /// </summary>
  /// <param name="albumId">The unique identifier of the album.</param>
  /// <param name="cancellationToken">Token to cancel the operation.</param>
  /// <returns>A task representing the download operation.</returns>
  public async Task DownloadAlbumAsync(string albumId, CancellationToken cancellationToken = default)
  ```

#### Code Style
- **Follow Microsoft C# conventions**
- **Use PascalCase** for public members
- **Use camelCase** for private fields and local variables
- **Use meaningful names** that describe intent
- **Keep methods focused** (single responsibility)
- **Async methods** should end with `Async` suffix

#### Error Handling
- **Use specific exceptions** rather than generic ones
- **Include meaningful error messages**
- **Log errors appropriately**
- **Handle cancellation tokens** in async methods

### TypeScript Frontend

#### Component Structure
- **Use functional components** with hooks
- **One component per file** with matching filename
- **Export as default** from component files
- **Group related components** in directories

#### Code Style
- **Follow ESLint rules** configured in the project
- **Use TypeScript types** for all props and state
- **Prefer interfaces** over type aliases for object shapes
- **Use meaningful component and variable names**

#### React Best Practices
- **Use hooks appropriately** (useState, useEffect, custom hooks)
- **Memoize expensive calculations** with useMemo
- **Debounce user input** for search and filters
- **Handle loading and error states** in components

### Database

#### Entity Framework
- **Use explicit migrations** for schema changes
- **Include descriptive migration names**
- **Test migrations** on sample data
- **Use navigation properties** appropriately

#### Entity Design
- **Use data annotations** for validation
- **Include XML documentation** on entity properties
- **Follow naming conventions** for database objects

## 🧪 Testing

### Backend Tests
```bash
cd backend
dotnet test
```

### Frontend Tests
```bash
cd frontend
npm test
```

### Integration Tests
- **Test API endpoints** with realistic data
- **Verify SignalR connectivity** and message flow
- **Test file upload/download** scenarios

## 📝 Pull Request Process

### Before Submitting

1. **Ensure tests pass**: Run full test suite
2. **Check code formatting**: Use IDE formatters
3. **Update documentation**: Include relevant changes
4. **Test manually**: Verify functionality works as expected

### PR Description Template

```markdown
## Description
Brief description of changes and motivation.

## Type of Change
- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update

## Testing
- [ ] Unit tests added/updated
- [ ] Integration tests added/updated
- [ ] Manual testing completed

## Screenshots
Include screenshots for UI changes.

## Checklist
- [ ] Code follows project style guidelines
- [ ] Self-review of code completed
- [ ] Code is commented appropriately
- [ ] Documentation updated as needed
- [ ] Tests added/updated as needed
- [ ] All tests pass
```

### Review Process

1. **Automated checks** must pass (build, tests, linting)
2. **Code review** by maintainers
3. **Manual testing** of changes when applicable
4. **Documentation review** for user-facing changes

## 🏗️ Architecture Guidelines

### Single Responsibility Principle

Each class should have one reason to change:

**Good**:
```csharp
public class UserService
{
    public async Task<User> GetUserAsync(int id) { }
    public async Task CreateUserAsync(User user) { }
}

public class AuthService  
{
    public async Task<string> AuthenticateAsync(string username, string password) { }
    public string GenerateJwtToken(User user) { }
}
```

**Avoid**:
```csharp
public class UserService
{
    // User management AND authentication AND email sending
    public async Task<User> GetUserAsync(int id) { }
    public async Task<string> AuthenticateAsync(string username, string password) { }
    public async Task SendWelcomeEmailAsync(User user) { }
}
```

### Dependency Injection

- **Register services** with appropriate lifetimes
- **Use interfaces** for service contracts
- **Avoid service locator pattern**
- **Keep constructors focused** on dependency injection

### Async/Await

- **Use async/await** for I/O bound operations
- **Don't block async code** with `.Result` or `.Wait()`
- **Use ConfigureAwait(false)** in library code
- **Handle cancellation tokens** appropriately

## 🔄 Release Process

### Versioning

We use [Semantic Versioning](https://semver.org/):
- **MAJOR**: Breaking changes
- **MINOR**: New features (backward compatible)
- **PATCH**: Bug fixes (backward compatible)

### Release Checklist

1. **Update version numbers** in relevant files
2. **Update CHANGELOG.md** with release notes
3. **Create release branch** from develop
4. **Run full test suite** on multiple environments
5. **Create GitHub release** with detailed notes
6. **Update Docker images** and documentation

## ❓ Getting Help

- **Discord/Slack**: Join our community chat
- **GitHub Discussions**: Ask questions and share ideas
- **GitHub Issues**: Report bugs and request features
- **Wiki**: Browse detailed documentation

## 🙏 Recognition

Contributors are recognized in:
- **README.md**: All contributors section
- **Release notes**: Major contributors highlighted
- **GitHub**: Contributor graphs and statistics

Thank you for contributing to Immich Downloader! 🎉