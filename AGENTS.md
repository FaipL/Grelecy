# Repository identity and privacy

Use only the public pseudonym `faipl` for commits and contribution trailers.
Use `289188090+FaipL@users.noreply.github.com` as the author and committer email.
Never copy a real name, personal email, ChatGPT profile identity or relay email
into commits, sign-offs, co-author trailers, pull requests, releases or build logs.

Before publishing, configure this repository's `user.name` and `user.email` and
verify the effective author and committer identity without logging any personal
identity. Explicitly set `GIT_AUTHOR_NAME`, `GIT_AUTHOR_EMAIL`,
`GIT_COMMITTER_NAME` and `GIT_COMMITTER_EMAIL` for commit operations when the
execution environment can override Git configuration.

When merging through GitHub, supply an explicit commit message containing only
public identities. Inspect author, committer and contribution trailers before
publishing. Do not reuse historical commits from before the privacy cleanup.
