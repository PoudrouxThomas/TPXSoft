@echo off
rem Windows shim: the harness entry point is a POSIX script, run through Git Bash.
bash "%~dp0dev" %*
