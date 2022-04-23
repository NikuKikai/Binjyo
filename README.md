# Binjyo

A simple screenshot tool inspired by SETUNA2.
Suitable for illustrators.

## Setup

1. Download binjyo.exe from [here](https://github.com/NikuKikai/Binjyo/releases).
2. Just click binjyo.exe. Nothing but an icon will appear in task tray. (打开什么都不会发生，只会在托盘里出现一个图标)

## Usage

### On everywhere

Operation | Shortkey | Mouse
--- | --- | ---
take screenshot | `Ctrl+Alt+A`  or `Ctrl+Win+A`|

### On a focused memo

Operation | Shortkey | Mouse
--- | --- | ---
copy & close | `Ctrl+x` | `Double click`
copy | `Ctrl+c`
save png | `s`
close | `Esc`
size down | `d` | `Ctrl+wheel down`
size up | `f` | `Ctrl+wheel up`
size reset | ` ` `
rotate | `r`
flip horizontally | `h`
flip vertically | `v`
grayscale | `g`
binarization on/off | `b`
binarization change threshold | `b + wheel`
quantization on/off <br>(not coexit with binarization) | `q`
quantization change number  | `b + wheel`
hue map | `c`
switch to greyscale | `g`
show color wheel | `shift` with mouse hovering on image

> `resize` is limited to min of 25 pixels and max of `screen width`

> `Save`, `cut` or `copy` applies to the displaying image with effects (expect scaling). 

Click the button on top-left corner to switch mode. (Maybe removed in future)
- normal
- locked
- minimized

## TODO

- Drawing
- Save, cut or copy scaled image (maybe binding to `Ctrl+...`)
- Keymaps, About in menu
- Quick save/ save all button in menu.


## Dependency(for Developer)

- .NET 4.7.2
- WPF
