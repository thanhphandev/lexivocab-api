# LexiVocab Internal Development Guide

This repository contains proprietary software. Access is restricted to authorized developers only.

## Security & Confidentiality

- **DO NOT** share the source code or configuration files with unauthorized parties.
- **DO NOT** commit plaintext secrets (API Keys, Connection Strings) to the repository. Use environment variables.
- Ensure all security hardening measures (Token Hashing, Rate Limiting) remain intact during development.

## Coding Standards

- **Clean Architecture & CQRS**: Maintain strict separation of layers.
- **MediatR**: All business logic must reside in Handlers.
- **Validation**: Every mutation command must have a FluentValidation validator.
- **Rich Domain Model**: Prefer moving core business logic into Domain Entities where appropriate.
- **Zero-Reflection**: Use the static virtual members on `IResult<T>` for error responses.

## PR Review Checklist

- [ ] Does it follow Clean Architecture?
- [ ] Are there unit tests for the core logic?
- [ ] Is the CancellationToken forwarded?
- [ ] Are database queries optimized (no N+1)?
- [ ] Does it pass the CI build?
