# LexiVocab Development Guide

Welcome to the LexiVocab project! This guide outlines our shared coding standards and practices to keep our codebase clean, scalable, and easy to work with.

## 🛠 Coding Standards

We follow modern architectural patterns to ensure the system remains maintainable:

*   **Clean Architecture & CQRS:** Maintain a strict separation between layers. Keep the core logic isolated from external concerns.
*   **MediatR:** All business logic should reside within **Handlers**. This keeps our controllers thin and our logic decoupled.
*   **Validation:** Every mutation command must be paired with a **FluentValidation** validator to ensure data integrity.
*   **Rich Domain Model:** Don't be afraid to move core business logic into **Domain Entities** where it naturally belongs.
*   **Zero-Reflection:** For performance, use the static virtual members on `IResult<T>` for error responses.

---

## 🔐 Security & Best Practices

Even in an open environment, keeping our app secure is a priority:

*   **Secret Management:** Never hardcode API Keys or Connection Strings. Use **environment variables** or local secret managers.
*   **Hardening:** Keep security measures like Token Hashing and Rate Limiting active during development to catch issues early.

---

## ✅ PR Review Checklist

Before submitting or approving a Pull Request, please verify:

- [ ] **Architecture:** Does it follow Clean Architecture principles?
- [ ] **Testing:** Are there unit tests covering the core logic?
- [ ] **Async Flow:** Is the `CancellationToken` properly forwarded?
- [ ] **Database:** Are queries optimized? (Watch out for the **N+1** problem).
- [ ] **CI Status:** Does the build pass all checks?

---

> **Note:** We aim for code that is easy to read and even easier to maintain. When in doubt, follow the existing patterns in the project!