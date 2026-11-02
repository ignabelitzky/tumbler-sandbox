# Unity Personal: local compilation

This page supersedes the previous GameCI activation setup.
Unity runs locally using Ignacio's existing activated Unity Hub account.
GitHub Actions never activates Unity and needs no Unity secrets.
Do not upload, convert or share any license XML/ULF or Unity credentials.

Follow [Local builds and releases](local-releases.md) for setup, native validation,
compilation, upload and automatic publication. The workflow only uses GitHub's
automatically supplied repository token. Old Unity secrets are unused and can
be removed privately in repository settings; the scripts never read them.
