# Branch Protection Setup Guide

This guide explains how to configure GitHub branch protection rules to require tests before merging pull requests.

## Required Branch Protection Rules

### 1. Access Repository Settings
1. Go to your GitHub repository
2. Click **Settings** tab
3. Navigate to **Branches** in the left sidebar

### 2. Add Branch Protection Rule for `main`

#### Basic Settings
- **Branch name pattern**: `main`
- **Protect matching branches**: ✅ Enabled

#### Required Status Checks
- ✅ **Require status checks to pass before merging**
- ✅ **Require branches to be up to date before merging**

**Required status checks (select these):**
- `Required Tests` ⭐ **CRITICAL - This is the main gate**
- `Unit Tests`
- `Component Tests` 
- `Frontend Tests`

**Optional status checks (recommended but not blocking):**
- `E2E Tests`
- `Docker Build Test`
- `All Tests Summary`

#### Pull Request Requirements
- ✅ **Require a pull request before merging**
- ✅ **Require approvals**: Set to `1` (or your preferred number)
- ✅ **Dismiss stale pull request approvals when new commits are pushed**
- ✅ **Require review from code owners** (if you have a CODEOWNERS file)

#### Additional Restrictions
- ✅ **Restrict pushes that create files**
- ✅ **Require signed commits** (optional, for additional security)
- ✅ **Require linear history** (optional, keeps history clean)
- ✅ **Include administrators** (applies rules to admins too)

### 3. Test the Configuration

#### Create a Test Pull Request
```bash
# Create a feature branch
git checkout -b test-branch-protection

# Make a small change
echo "# Test change" >> TEST.md
git add TEST.md
git commit -m "Test branch protection"

# Push and create PR
git push origin test-branch-protection
```

#### Verify Protection Works
1. Create the PR on GitHub
2. Confirm you see "Merging is blocked" message
3. Wait for tests to run (should take 5-15 minutes)
4. Once tests pass, "Merge pull request" button should become available
5. If any required test fails, merging should remain blocked

## GitHub Actions Workflow Configuration

### Test Execution Strategy

#### Required Tests (Fast - 3-5 minutes)
These **MUST pass** for PR merging:
- **Unit Tests**: Fast isolated tests without external dependencies
- **Component Tests**: Integration tests with mock services and test databases
- **Frontend Tests**: React/TypeScript unit tests with Jest

#### Optional Tests (Slower - 10-20 minutes)
These provide additional confidence but don't block merging:
- **E2E Tests**: Full application testing with Playwright
- **Docker Build Test**: Container image validation
- **Security Scan**: Only runs on push to main/develop (not PRs)

### Workflow Features

#### Performance Optimizations
- **Concurrency Control**: Cancels previous runs on new commits
- **Caching**: NuGet packages and npm dependencies cached
- **Parallel Execution**: Tests run in parallel for speed
- **Conditional Execution**: Security scans only on push to main

#### Error Handling
- **PostgreSQL Health Checks**: Ensures database is ready
- **Timeout Protection**: Prevents hung tests
- **Detailed Logging**: Verbose output for debugging failures
- **Artifact Collection**: Test results uploaded for analysis

### Status Check Configuration

#### Required Status Check: "Required Tests"
```yaml
required-tests:
  name: Required Tests
  needs: [unit-tests, component-tests, frontend-tests]
  # This job MUST pass for PR merging
```

#### Informational Status Check: "All Tests Summary"
```yaml
all-tests:
  name: All Tests Summary  
  needs: [unit-tests, component-tests, frontend-tests, e2e-tests, docker-build, security-scan]
  # This provides additional information but doesn't block merging
```

## Troubleshooting

### Common Issues

#### 1. Tests Are Not Running
**Problem**: Status checks don't appear on PR
**Solution**: 
- Ensure `.github/workflows/test.yml` exists in the main branch
- Check that workflow file syntax is valid
- Verify repository has Actions enabled

#### 2. Tests Always Fail
**Problem**: Component tests fail with database errors
**Solution**:
- Check PostgreSQL service configuration in workflow
- Verify test container startup sequence
- Review test logs in Actions tab

#### 3. Required Status Checks Not Found
**Problem**: Branch protection can't find status checks
**Solution**:
- Run workflow at least once to register status checks
- Ensure status check names match exactly
- Wait a few minutes for GitHub to refresh available checks

#### 4. E2E Tests Are Flaky
**Problem**: E2E tests pass locally but fail in CI
**Solution**:
- E2E tests are optional and don't block merging
- Review Playwright test logs and screenshots
- Consider increasing timeouts for CI environment

### Monitoring Test Performance

#### Expected Test Times
- **Unit Tests**: 30-60 seconds
- **Component Tests**: 2-3 minutes (includes PostgreSQL startup)
- **Frontend Tests**: 1-2 minutes
- **E2E Tests**: 5-10 minutes (full application stack)
- **Total Required Tests**: 3-6 minutes

#### Performance Alerts
If tests consistently take longer than expected:
1. Check for resource contention in GitHub Actions
2. Review test logs for slow queries or operations
3. Consider splitting large test suites
4. Optimize database operations in component tests

## Security Considerations

### Secrets and Environment Variables
- Database credentials are hardcoded for test environment (safe)
- No production secrets should be in test workflows
- Test data is automatically cleaned up

### Access Control
- Branch protection applies to all users (including admins)
- Status checks prevent bypass of testing requirements
- Signed commits add additional security layer

### Audit Trail
- All test runs are logged and retained
- Failed tests generate detailed artifacts
- Branch protection events are audited in repository logs

## Best Practices

### For Developers
1. **Run tests locally** before pushing: `dotnet test` and `npm test`
2. **Keep PRs small** to minimize test execution time
3. **Monitor test results** and fix failing tests promptly
4. **Use descriptive commit messages** for better test tracking

### For Maintainers
1. **Review test results** before approving PRs
2. **Monitor test performance** and optimize slow tests
3. **Update status check requirements** as the project evolves
4. **Regularly review branch protection settings**

### For Repository Admins
1. **Include administrators** in branch protection rules
2. **Require signed commits** for additional security
3. **Monitor workflow usage** and costs
4. **Backup and version control** branch protection settings

## Advanced Configuration

### Custom Status Checks
Add additional required checks by modifying the workflow:

```yaml
custom-quality-gate:
  name: Code Quality Gate
  needs: [required-tests]
  steps:
    - name: Check code coverage
      run: |
        # Add custom quality checks here
        echo "Code coverage: 85%"
```

### Matrix Testing
Test multiple configurations:

```yaml
component-tests:
  strategy:
    matrix:
      database: [postgres, sqlite]
      dotnet: ['8.0.x', '9.0.x']
```

### Conditional Requirements
Require different tests for different paths:

```yaml
backend-tests:
  if: contains(github.event.pull_request.changed_files, 'backend/')
  
frontend-tests:
  if: contains(github.event.pull_request.changed_files, 'frontend/')
```

## Summary

This branch protection configuration ensures:
- ✅ **Quality Gate**: No code reaches main without passing tests
- ✅ **Fast Feedback**: Required tests complete in 3-6 minutes
- ✅ **Comprehensive Coverage**: Optional tests provide additional validation
- ✅ **Security**: All changes are tested and reviewed
- ✅ **Reliability**: Flaky tests don't block development
- ✅ **Transparency**: Clear status indicators on all PRs

The configuration balances development velocity with code quality, ensuring that the main branch always contains tested, working code while providing fast feedback to developers.