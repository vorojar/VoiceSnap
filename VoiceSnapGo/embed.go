package main

import (
	"embed"
	"io/fs"
)

//go:embed frontend/dist
var embeddedAssets embed.FS

var assets, _ = fs.Sub(embeddedAssets, "frontend/dist")

//go:embed build/windows/icon.ico
var appIcon []byte

//go:embed build/darwin/tray_icon.png
var trayIcon []byte
