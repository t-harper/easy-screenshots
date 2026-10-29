# Validation

Native Windows check on Windows 11 build 26200, x64:

- Ctrl+Shift+4 registration succeeded.
- Duplicate registration rejected with Windows error 1409.
- Registration released and reacquired successfully.
- Desktop capture decoded as an opaque PNG, 7680 × 2160 pixels.
- Real app home window launched and exposed its controls through UI Automation.
- Sending Ctrl+Shift+4 hid the home window and began the selection session.

The automated desktop tool could not target the non-taskbar capture overlays. Full manual drag selection across different DPI settings, pointer alignment at monitor edges, PNG Save As dialog behavior and drag-out into Explorer/Codex/Claude remain interactive acceptance checks. Automated headless editor checks and native API checks do not establish those outcomes.

Latest Release verification: 27 tests passed (22 Core, 5 headless UI). The self-contained win-x64 single-file executable published successfully. Arrow rendering tests verify the filled head and short-arrow scaling; the generated style preview was visually inspected. The published executable also passed the native registration/capture smoke check before the arrow styling update.
