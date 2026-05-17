# Binjyo

A lightweight screenshot memo tool inspired by SETUNA2.  
It is designed for quickly capturing part of the screen, keeping the image on top, and doing simple visual inspection work.

## Setup

1. Download `binjyo.exe` from the [releases page](https://github.com/NikuKikai/Binjyo/releases).
2. Launch `binjyo.exe`.

The app stays in the system tray. It does not open a main window on startup.

## Tray Menu

The tray icon menu provides:

- `Minimize All`
- `Expand/Unlock All`
- `Close All`
- `History...`
- `Shortcut Help`
- `Settings...`
- `Exit`

## Screenshot Flow

1. Press the global screenshot shortcut.
2. Drag to select an area.
3. Release the mouse to create a `Memo`.
4. Work with the memo by keyboard or mouse.

The default global shortcut is `Ctrl+Alt+A`.  
You can change it in `Settings...`.

## History

- Every time a memo is closed, its current image, size, and position are saved to the system temporary folder.
- `History...` in the tray menu opens the history window.
- History entries are grouped by close date in descending order.
- Double-click an entry to restore it.
- Restored entries are removed from history after restoration.
- If the saved position is outside the current screen layout, the memo is moved back into a reachable visible area.
- The history window also has a fixed `Clear` button.

## Memo Operations

### Basic

Operation | Shortcut | Mouse
--- | --- | ---
Copy and close | `Ctrl+X` | `Double click`
Copy | `Ctrl+C`
Save PNG | `S`
Close | `Esc`
Reset size | `` ` ``

### Size / Transform

Operation | Shortcut | Mouse
--- | --- | ---
Resize smaller | `D` | `Ctrl + wheel down`
Resize larger | `F` | `Ctrl + wheel up`
Rotate 90° | `R`
Flip horizontally | `H`
Flip vertically | `V`

Notes:

- Resize is limited to a minimum visible size and a reasonable maximum based on the screen.
- Saving and copying use the current displayed image with visual effects applied, but scaling itself is display-only.

### Effects

Operation | Shortcut | Mouse
--- | --- | ---
Grayscale on/off | `G`
Binarization on/off | `B`
Binarization threshold | `B + wheel`
Quantization on/off | `Q`
Quantization level | `Q + wheel`
Opacity on/off | `O`
Opacity amount | `O + wheel`
Hue map on/off | `C`
Color wheel / picker info | `Shift` while hovering

The `Shift + hover` color popup shows:

- HSV
- RGB
- original-image coordinates under the mouse

The coordinate readout is mapped back to the original image space and is not affected by display scaling.

### Move / Snap

Operation | Shortcut | Mouse
--- | --- | ---
Move memo | `Arrow keys`
Move faster | `Shift + Arrow keys`
Jump to next snap | `Ctrl + Arrow keys`
Move connected memos | `Alt + Arrow keys` | `Alt + drag`
Temporarily invert snap |  | `Space + drag`

Behavior:

- Normal arrow-key movement does not snap.
- `Ctrl + Arrow keys` moves to the next snappable position in that direction.
- Snapping works against screen edges and other memos.
- Memo-to-memo snapping only happens when the relevant spans overlap or touch.
- While moving a connected group with `Alt`, snap calculations use the group's bounding box.
- When moving, every memo is kept reachable on screen with at least a small visible edge.

### Resize Mode

Operation | Shortcut | Mouse
--- | --- | ---
Toggle resize mode | `T`
Resize from corners |  | drag corner markers
Temporarily invert resize snap |  | `Space + drag`

Behavior:

- In resize mode, L-shaped corner markers appear inside the memo.
- Dragging a corner keeps the original aspect ratio.
- A center overlay shows the current scale percentage and displayed size while resizing.

### Focus

Operation | Shortcut
--- | ---
Focus memo under mouse / cycle overlap | `Tab`

Behavior:

- If the mouse is over one memo, `Tab` focuses that memo.
- If the mouse is over overlapping memos, `Tab` cycles through those memos.
- If the mouse is not over any memo, `Tab` cycles through all memos.
- When a memo gains focus, it flashes green briefly.

## Lock Button

Click the top-left button to switch mode:

- normal
- locked
- minimized

## Dependency (for Developers)

- .NET Framework 4.8
- WPF
