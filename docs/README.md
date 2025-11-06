# Immich Album Downloader - Documentation Index

Welcome to the comprehensive documentation for the Immich Album Downloader project.

## Documentation Overview

This documentation suite provides complete information about the project's architecture, API, deployment, and development.

### 📚 Documentation Files

#### [PROJECT_DOCUMENTATION.md](./PROJECT_DOCUMENTATION.md)
**Complete Project Overview and Technical Documentation**

Comprehensive documentation covering:
- Executive summary and project overview
- Complete system architecture
- Backend implementation details (106 files, 20,000+ lines)
- Frontend implementation details (31 files, 2,600+ lines)
- Database schema and relationships
- Security implementation (JWT, BCrypt, OWASP headers)
- Testing strategy (268+ tests, 100% pass rate)
- Performance optimizations
- Development guidelines

**Audience**: Developers, architects, technical leads

---

#### [API_DOCUMENTATION.md](./API_DOCUMENTATION.md)
**Complete REST API Reference**

Full API documentation including:
- Authentication flows (JWT Bearer)
- All API endpoints with examples
- Request/response schemas
- Data models and types
- Error handling and status codes
- Rate limiting policies
- SignalR real-time updates
- Complete workflow examples with curl commands

**Audience**: Frontend developers, API consumers, integration developers

---

#### [DEPLOYMENT_GUIDE.md](./DEPLOYMENT_GUIDE.md)
**Production Deployment and Operations Guide**

Complete deployment documentation:
- Prerequisites and system requirements
- Quick start guide
- Production deployment steps
- Environment configuration
- SSL/TLS setup (Let's Encrypt)
- Database management and backups
- Monitoring and logging
- Performance tuning
- Troubleshooting guide
- Security hardening
- Maintenance procedures

**Audience**: DevOps engineers, system administrators, deployment teams

---

## Quick Links

### Getting Started
- [Quick Start Guide](./DEPLOYMENT_GUIDE.md#quick-start)
- [Prerequisites](./DEPLOYMENT_GUIDE.md#prerequisites)
- [Initial Setup](./DEPLOYMENT_GUIDE.md#initial-configuration)

### Development
- [Backend Architecture](./PROJECT_DOCUMENTATION.md#backend-implementation)
- [Frontend Architecture](./PROJECT_DOCUMENTATION.md#frontend-implementation)
- [Development Guidelines](./PROJECT_DOCUMENTATION.md#development-guidelines)
- [Testing Strategy](./PROJECT_DOCUMENTATION.md#testing-strategy)

### API Integration
- [Authentication](./API_DOCUMENTATION.md#authentication)
- [API Endpoints](./API_DOCUMENTATION.md#api-endpoints)
- [SignalR Real-Time Updates](./API_DOCUMENTATION.md#signalr-real-time-updates)
- [Error Handling](./API_DOCUMENTATION.md#error-handling)

### Operations
- [Production Deployment](./DEPLOYMENT_GUIDE.md#production-deployment)
- [SSL/TLS Setup](./DEPLOYMENT_GUIDE.md#ssltls-setup)
- [Backup and Restore](./DEPLOYMENT_GUIDE.md#backup-and-restore)
- [Troubleshooting](./DEPLOYMENT_GUIDE.md#troubleshooting)

---

## Project Statistics

- **Backend**: 106 files, ~20,000+ lines of C# code
- **Frontend**: 31 files, ~2,600+ lines of TypeScript/React code
- **Test Coverage**: 268+ tests with 100% pass rate
- **Documentation**: 3 comprehensive guides, 10,000+ lines
- **Technology Stack**: .NET 9, React 18, TypeScript 5, SQLite, Docker

---

## Architecture Highlights

### Streaming Architecture
Memory-efficient design that can handle albums of any size:
- Disk-based processing (not memory)
- Chunked operations (50 images per chunk)
- Direct ZIP file streaming
- Concurrent download limiting (max 5)

### Security Features
Enterprise-grade security implementation:
- JWT authentication with HS256
- BCrypt password hashing
- Comprehensive security headers (OWASP)
- CORS configuration
- Input validation at multiple layers
- Path traversal protection
- Rate limiting

### Real-Time Updates
Live progress tracking:
- SignalR WebSocket integration
- Automatic reconnection
- Task status updates
- Progress percentage tracking

---

## Technology Stack

### Backend
- **.NET 9**: Latest ASP.NET Core
- **Entity Framework Core**: ORM with SQLite
- **SignalR**: Real-time communication
- **ImageSharp**: Image processing
- **BCrypt**: Password hashing
- **JWT Bearer**: Authentication

### Frontend
- **React 18**: Modern UI framework
- **TypeScript 5**: Type safety
- **Bootstrap 5**: Responsive design
- **Axios**: HTTP client
- **SignalR Client**: WebSocket
- **React Router**: Navigation

### Infrastructure
- **Docker**: Containerization
- **Docker Compose**: Orchestration
- **Nginx**: Reverse proxy
- **SQLite**: Database
- **GitHub Actions**: CI/CD

---

## Key Features

1. **Photo Frame Processing**
   - Intelligent black border padding
   - Aspect ratio preservation
   - Configurable JPEG quality (1-100%)

2. **Incremental Sync**
   - Smart detection of new photos
   - Cleanup of removed assets
   - Efficient re-downloads

3. **Background Processing**
   - Non-blocking operations
   - Bounded task queue (100 tasks)
   - Graceful cancellation

4. **Comprehensive Testing**
   - Unit tests
   - Component tests
   - Security tests
   - E2E tests
   - 268+ tests total

---

## Documentation Structure

```
docs/
├── README.md                    # This file - Documentation index
├── PROJECT_DOCUMENTATION.md     # Complete technical documentation
├── API_DOCUMENTATION.md         # REST API reference
└── DEPLOYMENT_GUIDE.md          # Deployment and operations guide
```

---

## Contributing

See [CONTRIBUTING.md](../CONTRIBUTING.md) in the root directory for:
- Development setup
- Coding standards
- Pull request process
- Testing requirements

---

## Support

- **Issues**: [GitHub Issues](https://github.com/dan-jeff/immich-album-downloader/issues)
- **Discussions**: [GitHub Discussions](https://github.com/dan-jeff/immich-album-downloader/discussions)

---

## License

This project is licensed under the Apache License 2.0. See [LICENSE](../LICENSE) for details.

---

## Acknowledgments

Special thanks to:
- [Immich](https://immich.app/) - The amazing self-hosted photo management solution
- [ImageSharp](https://sixlabors.com/products/imagesharp/) - Cross-platform image processing
- [SignalR](https://docs.microsoft.com/en-us/aspnet/core/signalr/) - Real-time web functionality
- All contributors and users of this project

---

**Documentation Version**: 1.0
**Last Updated**: 2025-11-06
**Project Version**: 1.0
**Status**: Complete

---

## Document Change Log

| Date | Version | Changes |
|------|---------|---------|
| 2025-11-06 | 1.0 | Initial comprehensive documentation release |

