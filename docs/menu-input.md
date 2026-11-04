# Menu input and Phosphor credit

The project now selects **Input System Package (New)** in Active Input Handling,
using the already installed Input System 1.20.1. Gameplay action IDs, generated
wrapper, drivetrain and vehicle physics are unchanged.

Main Menu already had an InputSystemUIInputModule, but allowed background clicks
to clear selection and had no general recovery for null/inactive selection.
Gamepad navigation needs an active selected button; mouse interaction can still
work when that selection is lost. The menu now preserves selection on background
clicks and recovers the active page's default button when selection is invalid.

`MenuInputNavigation` gives each scene a private copy of
`Resources/MenuInput.inputactions`. It assigns navigation, submit, cancel and
pointer actions explicitly to InputSystemUIInputModule, without a PlayerInput
device filter or control-scheme mask. A newly connected gamepad can join UI input
without restarting the scene. The module and actions are disabled during driving
and enabled for pause, including unscaled navigation while timeScale is zero.

| UI action | Xbox / Gamepad | Keyboard / mouse |
| --- | --- | --- |
| Navigate | Left stick or D-pad | Arrows or WASD |
| Confirm | A / South button | Enter, Numpad Enter, Space or left click |
| Back / resume | B / East button | Escape |

Back closes Controls and returns focus to Controls on the main page. On pause,
resume waits for A/B/Start/Enter/Space/Escape/mouse release so a UI confirmation
cannot become vehicle boost, braking or handbrake. The UI Cancel callback is
deferred until after EventSystem processing to avoid a page transition processing
the same UI event twice. Valid mouse-selected buttons are left selected.

The main menu uses the original transparent Phosphor PNG and the existing menu
font for a non-interactive bottom-left credit: **Developed by** on the first
line and **Ignacio Belitzky** on the second. The smaller first line and bold name
are left-aligned next to the icon, separated by 12 canvas units. The compact
group is 248 × 56 canvas units and the icon remains square. The group uses the
existing canvas scaler, the original author's 6.7%-from-left alignment and a
24-unit bottom inset. The old standalone author text is cleared and its object
disabled; the right-hand footer remains unobstructed. The credit does not
intercept pointer clicks or become a navigation target.

## Validation

| Check | Result in this change |
| --- | --- |
| UI bindings, action types, scene references, live input dependency graph | PASS, 58 static checks |
| Navigation / lighting / tunnel / impact preservation, metadata and GUIDs | PASS |
| C# syntax | PASS, 30 files: project scripts and the navigation verifier |
| Unity Editor startup | PASS; official Unity 6000.6.7f1 downloaded, extracted and executed |
| Native Unity compilation and Play Mode | BLOCKED before compilation by licensing and Package Manager IPC initialization |
| Physical Xbox device, reconnection and focus switching | NOT RUN |
| Native menu render / Player build | NOT RUN |

The native command was actually attempted using the matching Linux Editor. Its
log confirms `Unity Editor version: 6000.6.7f1 (b22767f04f04)` and then reports
`Licensing initialization failed` and `Could not connect to IPC stream` for
Package Manager, followed by exit code 1. The environment rejected an escalation
request to run that same verifier outside the process isolation. No gameplay
or UI assertion ran, and the temporary verifier assets were cleaned up.

Run the available static suite from the repository root:

```sh
python3 tools/verify-navigation.py
python3 tools/sandbox-expansion/check.py
```

The extended native navigation harness sends virtual Input System events through
the real EventSystem rather than invoking button.onClick directly. It covers
controller-only menu navigation, D-pad/stick, submit/cancel, lost selection,
background clicks, keyboard/mouse coexistence, gamepad removal/reconnection,
Controls/Back, pause, return/re-entry and held-input isolation. It also checks the
credit's original artwork, text, anchor and non-interactive graphics.

With Unity 6000.6.7f1 installed and this project closed in the Editor:

```sh
python3 tools/verify-navigation.py --unity /absolute/path/to/6000.6.7f1/Editor/Unity
```

Then check a physical controller before and after connecting it with the menu
already open, after clicking the background, after opening/closing Controls,
after Alt-Tab, and after returning from Sandbox. These native and hardware checks
are pending; static results are not a claim that they ran.
