@echo off
:: Anti-PRISM One-Click Installer for Windows CMD
title Anti-PRISM Automated Installer
powershell -ExecutionPolicy Bypass -NoProfile -Command "irm https://raw.githubusercontent.com/peterfu0615/Anti-PRISM/main/scripts/install.ps1 | iex"
pause
