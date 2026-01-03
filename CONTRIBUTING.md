# Contributing to PushStream

First off, thank you for considering contributing to PushStream! 🎉

This document provides guidelines and steps for contributing. Following these guidelines helps communicate that you respect the time of the developers managing and developing this open source project.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [How Can I Contribute?](#how-can-i-contribute)
- [Development Setup](#development-setup)
- [Style Guidelines](#style-guidelines)
- [Commit Messages](#commit-messages)
- [Pull Request Process](#pull-request-process)

## Code of Conduct

This project and everyone participating in it is governed by our [Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code.

## Getting Started

PushStream is an opinionated abstraction over Server-Sent Events (SSE). Before contributing, please:

1. Read the [README](README.md) to understand the project's purpose
2. Review the [documentation](docs/) to understand design decisions
3. Check existing [issues](../../issues) to see if your concern is already being addressed

## How Can I Contribute?

### Reporting Bugs

Before creating bug reports, please check existing issues to avoid duplicates. When creating a bug report, include as many details as possible:

- **Use a clear and descriptive title**
- **Describe the exact steps to reproduce the problem**
- **Provide specific examples** (code snippets, configuration, etc.)
- **Describe the behavior you observed and expected**
- **Include your environment details** (.NET version, OS, browser if applicable)

### Suggesting Enhancements

Enhancement suggestions are tracked as GitHub issues. When creating an enhancement suggestion:

- **Use a clear and descriptive title**
- **Provide a detailed description of the proposed feature**
- **Explain why this enhancement would be useful**
- **Consider the project's design philosophy** — PushStream is intentionally opinionated and focused on simplicity

### Pull Requests

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Make your changes
4. Run tests to ensure nothing is broken
5. Commit your changes (see [Commit Messages](#commit-messages))
6. Push to your branch (`git push origin feature/amazing-feature`)
7. Open a Pull Request

## Development Setup

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- [Node.js 18+](https://nodejs.org/) (for JavaScript client development)
- Your preferred IDE (Visual Studio, VS Code, Rider)

### Building the Project

```bash
# Clone your fork
git clone https://github.com/YOUR_USERNAME/PushStream.git
cd PushStream

# Restore dependencies
dotnet restore

# Build all projects
dotnet build

# Run tests
dotnet test
```

### JavaScript Client Development

```bash
cd src/client/pushstream-js
npm install
npm run build
npm test
```

## Style Guidelines

### C# Code Style

- Follow [Microsoft's C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use meaningful, descriptive names
- Keep methods small and focused
- Write XML documentation for public APIs
- Use `async/await` consistently for asynchronous code

### TypeScript/JavaScript Code Style

- Use TypeScript for new code
- Follow the existing code style in the project
- Use meaningful, descriptive names
- Document public APIs with JSDoc comments

### General Guidelines

- Write self-documenting code; comments should explain "why", not "what"
- Keep the API surface minimal — add features only when necessary
- Maintain backward compatibility when possible
- Add tests for new functionality

## Commit Messages

We follow [Conventional Commits](https://www.conventionalcommits.org/) specification:

```
<type>(<scope>): <description>

[optional body]

[optional footer(s)]
```

### Types

- `feat`: A new feature
- `fix`: A bug fix
- `docs`: Documentation only changes
- `style`: Code style changes (formatting, missing semi-colons, etc.)
- `refactor`: Code change that neither fixes a bug nor adds a feature
- `perf`: Performance improvements
- `test`: Adding or correcting tests
- `chore`: Changes to build process or auxiliary tools

### Examples

```
feat(core): add connection timeout configuration

fix(client): handle reconnection on network change

docs: update getting started guide

test(publisher): add unit tests for broadcast functionality
```

## Pull Request Process

1. **Ensure your PR addresses a single concern** — don't mix features with bug fixes
2. **Update documentation** if you're changing public APIs or behavior
3. **Add tests** for new functionality
4. **Ensure all tests pass** before requesting review
5. **Keep the PR focused and small** — large PRs are harder to review
6. **Respond to feedback** in a timely manner

### PR Title Format

Use the same format as commit messages:

```
feat(core): add support for custom event serialization
```

### PR Description Template

```markdown
## Description
Brief description of the changes

## Related Issue
Fixes #123

## Type of Change
- [ ] Bug fix
- [ ] New feature
- [ ] Breaking change
- [ ] Documentation update

## Checklist
- [ ] My code follows the project's style guidelines
- [ ] I have performed a self-review of my code
- [ ] I have added tests that prove my fix/feature works
- [ ] New and existing tests pass locally
- [ ] I have updated the documentation accordingly
```

## Questions?

Feel free to open an issue with the `question` label if you have any questions about contributing.

---

Thank you for contributing to PushStream! 🚀