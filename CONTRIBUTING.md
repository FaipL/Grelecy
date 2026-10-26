<!-- SPDX-License-Identifier: CC-BY-4.0 -->

# Contributing to GRELECY

GRELECY is forming its community. Our most useful early contributions are hardware information, reproducible problems, research, and clear documentation. You do not need development experience to take part.

## Initial community scope

Owners, testers, and developers interested in **Kepler, Maxwell, or Pascal** GPUs, or **Turing-based GeForce GTX 16 series** cards on Windows are welcome from the start. Initial examples include Kepler-based GTX 600/700, Maxwell-based GTX 900, Pascal-based GeForce GTX 10 series, and Turing-based GeForce GTX 16 series cards. Report the exact model, desktop or laptop variant, and architecture where known. No generation has a predetermined implementation priority or guaranteed support. See [Project scope](docs/PROJECT_SCOPE.md).

## Choose a starting point

- [Introduce yourself and your hardware](https://github.com/FaipL/Grelecy/issues/new?template=01-join.yml).
- [Describe a compatibility issue](https://github.com/FaipL/Grelecy/issues/new?template=02-compatibility.yml).
- [Propose a research question](https://github.com/FaipL/Grelecy/issues/new?template=03-proposal.yml).
- Improve documentation with a focused pull request.

Search existing issues before opening a new one. English helps keep shared research accessible internationally; French is also welcome.

## Useful technical reports

Include the exact GPU model, desktop or laptop variant, architecture if known, Windows edition and build, driver version and source, application version, and clear reproduction steps.

Separate what you observed from what you infer. Include expected and actual behavior, references, and comparisons where available. "Not tested" and "unknown" are useful answers; do not guess hardware details.

Review logs and screenshots before uploading them. Remove account names, serial numbers, license keys, and other personal information that is not necessary to understand the issue.

## Research and software proposals

Start with an issue for substantial work so contributors can discuss its value and feasibility. Explain:

1. The specific problem or question.
2. Existing approaches and primary sources.
3. The hardware and skills needed to investigate it.
4. A bounded experiment and a way to evaluate its result.
5. Dependencies, distribution rights, and likely maintenance needs.

A proposal is not a release commitment. Technical goals will be discussed with the community.

## Pull requests

Keep changes focused and explain the problem, your approach, and the evidence supporting it. Link related issues. For documentation, check accuracy and links; for software, explain how it was built and tested and list untested environments.

Preserve existing license and author notices. Use an SPDX notice appropriate to the material:

- Original software: MPL-2.0.
- Original documentation: CC-BY-4.0.
- Third-party material: its own license, clearly identified and reviewed for compatibility.

See [LICENSING.md](LICENSING.md) before contributing code or assets. Contributors keep their copyright; no assignment is required.

## Developer Certificate of Origin

Please sign off commits submitted in pull requests to certify that you have the right to contribute the material. Read the **[Developer Certificate of Origin 1.1](https://developercertificate.org/)** before signing.

Git can add the sign-off trailer with:

```sh
git commit -s
```

This produces a trailer in the form:

```text
Signed-off-by: Your Name <your-email@example.com>
```

A sign-off is a certification of contribution rights, not a cryptographic signature or copyright transfer. Use an identity and email address you are entitled to use, and remember that commit metadata and sign-offs are public. This is a contribution policy reviewed by maintainers; no automated DCO check is configured in this initial setup.

Introductions and ordinary issue reports do not require a signed Git commit.

## Respect third-party rights

Contribute only material you have the right to share. Do not upload leaked source code, proprietary drivers, firmware, or third-party assets without established permission. Cite and link legitimate upstream sources instead.

Research involving proprietary software must take account of its applicable terms and the relevant law. GRELECY's own licenses do not authorize modification or redistribution of third-party material.

## Community conduct

Be respectful, welcome newcomers, and discuss evidence and ideas constructively. Harassment, discrimination, personal attacks, spam, and publication of others' private information are not acceptable.

Maintainers may moderate contributions and restrict participation to keep this repository useful and welcoming. Use GitHub's reporting tools where appropriate. Avoid posting private incident details in public issues.

## Initial maintenance

[FaipL](https://github.com/FaipL) maintains this repository during community formation. Proposals and technical priorities are open to community discussion; the maintainer handles merge and repository decisions at this stage.

