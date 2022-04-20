# Binjyo

A simple screenshot tool inspired by SETUNA2.
Suitable for illustrators.

## Setup

1. Download binjyo.exe from [here](https://github.com/NikuKikai/Binjyo/releases).
2. Just click binjyo.exe. Nothing but an icon will appear in task tray. (打开什么都不会发生，只会在托盘里出现一个图标)

## Usage

On everywhere

Operation | Shortkey | Mouse
--- | --- | ---
take screenshot | `Ctrl+Alt+A`  or `Ctrl+Win+A`|

On a focused memo

Operation | Shortkey | Mouse
--- | --- | ---
copy & close | `Ctrl+x` | `Double click`
copy | `Ctrl+c`
close | `Esc`
size down | `d` | `Ctrl+wheel down`
size up | `f` | `Ctrl+wheel up`
size reset | `r`
switch to greyscale | `g`
save png | `s`
show color wheel | `shift`

> `resize` is limited to min of 25 pixels and max of `screen width`

> When greyscale showing, save, cut or copy applies to the greyscale image. 

Click the button on top-left corner to switch mode. (Maybe removed in future)
- normal
- locked
- minimized

## TODO

- Binarization (maybe binding to `b`)
- Save, cut or copy scaled image (maybe binding to `Ctrl+...`)
- Keymaps, About in menu
- Quick save/ save all button in menu.


## Dependency(for Developer)

- .NET 4.7.2
- WPF
