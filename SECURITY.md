# Security policy

## Reporting a vulnerability

Use GitHub's private vulnerability reporting on this repository — the **Report a vulnerability**
button under the Security tab — to send a report privately to the maintainer. That is the only
channel: please do not open a public issue, and do not post the details anywhere public before a fix
has shipped. A useful report says what you ran, what happened, what you expected, and on which
platform and version (`lunate --version`).

This is a small, personal, MIT-licensed project: there is no bounty and no response-time promise.
Reports are read and answered as quickly as the maintainer can, and credit is given in the release
notes unless you ask otherwise.

## The security posture

Lunate runs with your user privileges and asks before writes and commands; its own state is guarded
at the OS level, and it is not a sandbox. The full posture — approvals, the workspace boundary, the
bash guard, the container boundary, repository code, extensions, secrets and what is out of scope —
is on [Run Lunate safely](docs/security.md). This file is the reporting policy.
