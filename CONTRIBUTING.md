# Contributing to ClaudeForge

Thank you for your interest in contributing to ClaudeForge! This guide explains how to participate, whether you're reporting issues, submitting ideas, or contributing code.

## Getting Started

1. **Check existing issues** before reporting a new one—your concern may already be discussed.
2. **Read the README** to understand the project's scope and early-stage status.
3. **Familiarize yourself with the license**: ClaudeForge code is under [PolyForm Noncommercial 1.0.0](LICENSE). By contributing, you agree that your contributions are licensed under the same terms.

## How to Contribute

### Report Issues

Use GitHub Issues to report:
- Bugs or unexpected behavior
- Requests for new features or artefact categories
- Documentation gaps or confusion

**Please include:**
- Clear description of the issue
- Steps to reproduce (for bugs)
- Expected vs. actual behavior
- Environment or context (if relevant)

### Submit Ideas & Discussion

Open a GitHub Discussion or Issue to propose:
- New artefact types or categories
- Platform features or improvements
- Best practices for sharing and using artefacts

### Submit Code & Pull Requests

1. **Fork the repository** and create a feature branch:
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Write clean, focused code:**
   - Keep commits small and atomic
   - Use [Conventional Commits](https://www.conventionalcommits.org/) format:
     ```
     feat: add support for MCP server discovery
     fix: resolve timeout in artefact upload
     docs: clarify license model in README
     ```
   - Include tests for new functionality
   - Follow the project's code style

### Frontend Styling Rule (mandatory)

**Custom SCSS MUST reference CSS variable tokens for all colors — never hardcoded hex, RGB, or HSL values.**

```scss
// CORRECT — references the semantic token
.my-component {
  background-color: var(--primary);
  color: var(--primary-foreground);
  border-color: var(--border);
}

// WRONG — hardcoded values break dark mode and design-system consistency
.my-component {
  background-color: #f5c800;
  color: #1a1a1a;
  border-color: #e5e5e5;
}
```

All semantic tokens are defined in `frontend/src/theme.css` and documented in
`frontend/docs/DESIGN_TOKENS.md`. The full list of available tokens includes:
`--primary`, `--primary-foreground`, `--background`, `--foreground`, `--card`,
`--secondary`, `--secondary-foreground`, `--muted`, `--muted-foreground`,
`--accent`, `--accent-foreground`, `--destructive`, `--border`, `--input`, `--ring`.

This rule ensures:
- Dark mode works automatically (tokens resolve to the correct palette when `.dark` is present)
- A single change in `theme.css` propagates to all components
- WCAG AA contrast compliance is maintained (validated token values only)

3. **Test before submitting:**
   - Run tests and linting locally
   - Verify the build passes
   - Test your changes thoroughly

4. **Submit a pull request:**
   - Link any related issues (`Closes #123`)
   - Describe what changes and why
   - Request review from maintainers

5. **Address feedback:**
   - Respond to review comments
   - Push updates to your branch (do not force-push unless requested)
   - Engage respectfully with reviewers

### Contribute Artefacts

To share Claude agentic artefacts on the Platform (once it launches):
- Follow the Platform's submission guidelines
- Document your artefact clearly (purpose, usage, dependencies)
- Include examples or screenshots if helpful
- License your artefact appropriately (you remain the owner)

## Developer Certificate of Origin

By contributing, you affirm that:
1. The contribution is your own original work, or you have permission to submit it.
2. Your contribution does not infringe any third-party intellectual property rights.
3. You agree that your contribution is licensed under the PolyForm Noncommercial License 1.0.0 (same as the project).

You can signify this by including a "Signed-off-by" line in your commit:
```
git commit -m "feat: add feature

Signed-off-by: Your Name <your.email@example.com>"
```

## Code of Conduct

- Be respectful and inclusive of all contributors
- Assume good intent in discussions
- Focus feedback on the code, not the person
- Help others learn and grow

## Questions?

- Open a GitHub Issue or Discussion
- Contact the author: [Florian Michel](https://github.com/fmflurry) ([florianmichel@groupeisagri.com](mailto:florianmichel@groupeisagri.com))

Thank you for helping build ClaudeForge!
