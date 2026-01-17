---
allowed-tools: Read, Write
description: Update documentation in Docs folder
argument-hint: [file-name] [desired-change-description]
---

Update the documentation in /docs folder as it's described in $ARGUMENTS:
- [file-name] tells which file to update. If it doesn't have an extension, assume it's .md file.
- If there is a file with the same name but .cs extension, you should work on both [file-name].md and [file-name].cs pair.
- [desired-change-description] the change to make.
