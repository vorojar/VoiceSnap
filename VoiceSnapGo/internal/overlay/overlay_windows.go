//go:build windows

package overlay

import (
	"math"
	"runtime"
	"syscall"
	"time"
	"unsafe"
)

// ---- Win32 constants ----
const (
	wsPopup         = 0x80000000
	wsExLayered     = 0x00080000
	wsExTopmost     = 0x00000008
	wsExToolWindow  = 0x00000080
	wsExTransparent = 0x00000020
	wsExNoActivate  = 0x08000000
	swShow          = 5
	swHide          = 0
	ulwAlpha        = 0x02
	acSrcOver       = 0x00
	acSrcAlpha      = 0x01
	pmRemove        = 0x0001
	swpNoSize       = 0x0001
	swpNoZOrder     = 0x0004
	swpNoActivate   = 0x0010

	// Window messages for drag
	wmMove          = 0x0003
	wmNCHitTest     = 0x0084
	wmEnterSizeMove = 0x0231
	wmExitSizeMove  = 0x0232
	htCaption       = 2
)

// GDI+ constants
const (
	smoothingAntiAlias8x8 = 5
	textHintAntiAlias     = 4
	unitPixel             = 2
	fontStyleRegular      = 0
	fillModeAlternate     = 0
	strAlignNear          = 0
	strAlignCenter        = 1
)

// Colors (ARGB)
const (
	clrCapsuleBG = 0xD91C1C1E
	clrBorder    = 0x20FFFFFF
	clrText      = 0xFFB4B4B4
	clrBlue      = 0xFF007AFF
	clrGreen     = 0xFF34C759
	clrRed       = 0xFFFF3B30
	clrOrange    = 0xFFFF9500
)

// Dimensions (logical px)
const (
	capW   = 170
	capH   = 48
	capR   = 24
	barW   = 5
	barGap = 4
	barR   = 2.5
	nBars  = 5
	txtSz  = 12.0
	txtGap = 12
)

// ---- Win32 structs ----
type wPoint struct{ x, y int32 }
type wSize struct{ cx, cy int32 }
type wBlend struct{ op, flags, srcAlpha, alphaFmt byte }
type wBmpInfoHdr struct {
	biSize, biWidth, biHeight int32
	biPlanes, biBitCount      uint16
	biCompression             uint32
	biSizeImage               uint32
	biXPPM, biYPPM            int32
	biClrUsed, biClrImportant uint32
}
type wBmpInfo struct {
	hdr    wBmpInfoHdr
	colors [1]uint32
}
type wMsg struct {
	hwnd    uintptr
	message uint32
	wParam  uintptr
	lParam  uintptr
	time    uint32
	pt      wPoint
}
type wWndClassEx struct {
	cbSize      uint32
	style       uint32
	wndProc     uintptr
	clsExtra    int32
	wndExtra    int32
	hInstance   uintptr
	hIcon       uintptr
	hCursor     uintptr
	hbrBg       uintptr
	menuName    *uint16
	className   *uint16
	hIconSm     uintptr
}
type gdipSI struct {
	ver      uint32
	debugCb  uintptr
	noThread int32
	noCodecs int32
}
type rectF struct{ x, y, w, h float32 }

// ---- DLL procs ----
var (
	user32   = syscall.NewLazyDLL("user32.dll")
	gdi32    = syscall.NewLazyDLL("gdi32.dll")
	kernel32 = syscall.NewLazyDLL("kernel32.dll")
	gdipDLL  = syscall.NewLazyDLL("gdiplus.dll")

	uRegisterClassEx    = user32.NewProc("RegisterClassExW")
	uCreateWindowEx     = user32.NewProc("CreateWindowExW")
	uDestroyWindow      = user32.NewProc("DestroyWindow")
	uShowWindow         = user32.NewProc("ShowWindow")
	uSetWindowPos       = user32.NewProc("SetWindowPos")
	uUpdateLayeredWin   = user32.NewProc("UpdateLayeredWindow")
	uDefWindowProc      = user32.NewProc("DefWindowProcW")
	uGetDC              = user32.NewProc("GetDC")
	uReleaseDC          = user32.NewProc("ReleaseDC")
	uPeekMessage        = user32.NewProc("PeekMessageW")
	uTranslateMessage   = user32.NewProc("TranslateMessage")
	uDispatchMessage    = user32.NewProc("DispatchMessageW")
	kGetModuleHandle    = kernel32.NewProc("GetModuleHandleW")
	gCreateCompatibleDC = gdi32.NewProc("CreateCompatibleDC")
	gDeleteDC           = gdi32.NewProc("DeleteDC")
	gCreateDIBSection   = gdi32.NewProc("CreateDIBSection")
	gSelectObject       = gdi32.NewProc("SelectObject")
	gDeleteObject       = gdi32.NewProc("DeleteObject")

	gpStartup          = gdipDLL.NewProc("GdiplusStartup")
	gpShutdown         = gdipDLL.NewProc("GdiplusShutdown")
	gpFromHDC          = gdipDLL.NewProc("GdipCreateFromHDC")
	gpDeleteGraphics   = gdipDLL.NewProc("GdipDeleteGraphics")
	gpSetSmoothing     = gdipDLL.NewProc("GdipSetSmoothingMode")
	gpSetTextHint      = gdipDLL.NewProc("GdipSetTextRenderingHint")
	gpClear            = gdipDLL.NewProc("GdipGraphicsClear")
	gpCreateSolidFill  = gdipDLL.NewProc("GdipCreateSolidFill")
	gpDeleteBrush      = gdipDLL.NewProc("GdipDeleteBrush")
	gpCreatePath       = gdipDLL.NewProc("GdipCreatePath")
	gpDeletePath       = gdipDLL.NewProc("GdipDeletePath")
	gpAddPathArc       = gdipDLL.NewProc("GdipAddPathArc")
	gpClosePathFigure  = gdipDLL.NewProc("GdipClosePathFigure")
	gpFillPath         = gdipDLL.NewProc("GdipFillPath")
	gpDrawPath         = gdipDLL.NewProc("GdipDrawPath")
	gpCreatePen        = gdipDLL.NewProc("GdipCreatePen1")
	gpDeletePen        = gdipDLL.NewProc("GdipDeletePen")
	gpCreateFontFamily = gdipDLL.NewProc("GdipCreateFontFamilyFromName")
	gpDeleteFontFamily = gdipDLL.NewProc("GdipDeleteFontFamily")
	gpCreateFont       = gdipDLL.NewProc("GdipCreateFont")
	gpDeleteFont       = gdipDLL.NewProc("GdipDeleteFont")
	gpCreateStrFmt     = gdipDLL.NewProc("GdipCreateStringFormat")
	gpDeleteStrFmt     = gdipDLL.NewProc("GdipDeleteStringFormat")
	gpSetStrFmtAlign   = gdipDLL.NewProc("GdipSetStringFormatAlign")
	gpSetStrFmtLineAl  = gdipDLL.NewProc("GdipSetStringFormatLineAlign")
	gpDrawString       = gdipDLL.NewProc("GdipDrawString")
	gpMeasureString    = gdipDLL.NewProc("GdipMeasureString")
)

// getSystemDPI returns the system DPI (96 = 100%, 120 = 125%, 144 = 150%, 192 = 200%).
func getSystemDPI() int {
	// Try GetDpiForSystem (Win10 1607+, user32.dll)
	proc := user32.NewProc("GetDpiForSystem")
	if err := proc.Find(); err == nil {
		dpi, _, _ := proc.Call()
		if dpi > 0 {
			return int(dpi)
		}
	}
	// Fallback: GetDeviceCaps(LOGPIXELSX) — works on all Windows versions
	hdc, _, _ := uGetDC.Call(0)
	if hdc != 0 {
		defer uReleaseDC.Call(0, hdc)
		getDeviceCaps := gdi32.NewProc("GetDeviceCaps")
		dpi, _, _ := getDeviceCaps.Call(hdc, 88) // LOGPIXELSX = 88
		if dpi > 0 {
			return int(dpi)
		}
	}
	return 96
}

// f32 converts float32 to uintptr (for GDI+ flat API syscalls).
func f32(v float32) uintptr { return uintptr(math.Float32bits(v)) }

var gOverlay *winOverlay

func overlayWndProc(hwnd uintptr, msg uint32, wp, lp uintptr) uintptr {
	o := gOverlay
	switch msg {
	case wmNCHitTest:
		return htCaption
	case wmEnterSizeMove:
		if o != nil {
			o.dragging = true
		}
	case wmExitSizeMove:
		if o != nil {
			o.dragging = false
			if o.dragCb != nil {
				x, y := o.posX, o.posY
				go o.dragCb(x, y)
			}
		}
	case wmMove:
		if o != nil && o.dragging {
			o.posX = int(int16(lp & 0xFFFF))
			o.posY = int(int16((lp >> 16) & 0xFFFF))
		}
	}
	r, _, _ := uDefWindowProc.Call(hwnd, uintptr(msg), wp, lp)
	return r
}

func statusColor(s Status) uint32 {
	switch s {
	case StatusLoading:
		return clrBlue
	case StatusReady, StatusDone:
		return clrGreen
	case StatusRecording, StatusError:
		return clrRed
	case StatusFreetalking:
		return clrBlue
	case StatusProcessing, StatusCancelled, StatusNoVoice, StatusNoContent:
		return clrOrange
	default:
		return clrBlue
	}
}

// ---- Overlay struct ----

type statusCmd struct {
	status Status
	text   string
}

type winOverlay struct {
	showCh   chan bool
	statusCh chan statusCmd
	volCh    chan float64
	posCh    chan [2]int
	closeCh  chan struct{}
	done     chan struct{}

	barHeights [nBars]float64
	animTime   float64

	// Position & drag
	posX, posY int
	dragging   bool
	dragCb     func(x, y int)

	// DPI-scaled dimensions (set once in run())
	scale   float64
	sW, sH  int32   // scaled window width, height
	sCapR   float64 // scaled corner radius
	sBarW   float64 // scaled bar width
	sBarGap float64 // scaled bar gap
	sBarR   float64 // scaled bar radius
	sTxtSz  float32 // scaled font size
	sTxtGap float64 // scaled text gap
}

func New() Overlay {
	o := &winOverlay{
		showCh:   make(chan bool, 8),
		statusCh: make(chan statusCmd, 8),
		volCh:    make(chan float64, 32),
		posCh:    make(chan [2]int, 8),
		closeCh:  make(chan struct{}),
		done:     make(chan struct{}),
	}
	for i := range o.barHeights {
		o.barHeights[i] = 14
	}
	go o.run()
	return o
}

func (o *winOverlay) Show()                           { trySend(o.showCh, true) }
func (o *winOverlay) Hide()                           { trySend(o.showCh, false) }
func (o *winOverlay) SetStatus(s Status, text string) { trySendS(o.statusCh, statusCmd{s, text}) }
func (o *winOverlay) SetVolume(vol float64)           { trySendF(o.volCh, vol) }
func (o *winOverlay) SetPosition(x, y int)            { trySendP(o.posCh, [2]int{x, y}) }
func (o *winOverlay) GetPosition() (int, int)         { return o.posX, o.posY }
func (o *winOverlay) OnDragged(fn func(x, y int))     { o.dragCb = fn }
func (o *winOverlay) Close()                          { close(o.closeCh); <-o.done }

func trySend(ch chan bool, v bool)          { select { case ch <- v: default: } }
func trySendS(ch chan statusCmd, v statusCmd) { select { case ch <- v: default: } }
func trySendF(ch chan float64, v float64)   { select { case ch <- v: default: } }
func trySendP(ch chan [2]int, v [2]int)     { select { case ch <- v: default: } }

// ---- Main render thread ----

func (o *winOverlay) run() {
	defer close(o.done)
	runtime.LockOSThread()
	defer runtime.UnlockOSThread()

	// GDI+ init
	var token uintptr
	si := gdipSI{ver: 1}
	gpStartup.Call(uintptr(unsafe.Pointer(&token)), uintptr(unsafe.Pointer(&si)), 0)
	defer gpShutdown.Call(token)

	gOverlay = o

	// Compute DPI-scaled dimensions
	o.scale = float64(getSystemDPI()) / 96.0
	o.sW = int32(math.Round(float64(capW) * o.scale))
	o.sH = int32(math.Round(float64(capH) * o.scale))
	o.sCapR = capR * o.scale
	o.sBarW = barW * o.scale
	o.sBarGap = barGap * o.scale
	o.sBarR = barR * o.scale
	o.sTxtSz = float32(txtSz * o.scale)
	o.sTxtGap = txtGap * o.scale

	// Register window class
	hInst, _, _ := kGetModuleHandle.Call(0)
	cls, _ := syscall.UTF16PtrFromString("VoiceSnapOverlay")
	wc := wWndClassEx{
		cbSize:    uint32(unsafe.Sizeof(wWndClassEx{})),
		wndProc:   syscall.NewCallback(overlayWndProc),
		hInstance: hInst,
		className: cls,
	}
	uRegisterClassEx.Call(uintptr(unsafe.Pointer(&wc)))

	// Create layered window
	exStyle := uintptr(wsExLayered | wsExTopmost | wsExToolWindow | wsExNoActivate)
	hwnd, _, _ := uCreateWindowEx.Call(
		exStyle, uintptr(unsafe.Pointer(cls)), 0, wsPopup,
		0, 0, uintptr(o.sW), uintptr(o.sH), 0, 0, hInst, 0,
	)
	if hwnd == 0 {
		return
	}
	defer uDestroyWindow.Call(hwnd)

	// Memory DC + DIB
	screenDC, _, _ := uGetDC.Call(0)
	defer uReleaseDC.Call(0, screenDC)
	memDC, _, _ := gCreateCompatibleDC.Call(screenDC)
	defer gDeleteDC.Call(memDC)

	bi := wBmpInfo{hdr: wBmpInfoHdr{
		biSize:   int32(unsafe.Sizeof(wBmpInfoHdr{})),
		biWidth:  o.sW,
		biHeight: -o.sH, // top-down
		biPlanes: 1, biBitCount: 32,
	}}
	var bits unsafe.Pointer
	hBmp, _, _ := gCreateDIBSection.Call(memDC, uintptr(unsafe.Pointer(&bi)), 0, uintptr(unsafe.Pointer(&bits)), 0, 0)
	if hBmp == 0 {
		return
	}
	defer gDeleteObject.Call(hBmp)
	gSelectObject.Call(memDC, hBmp)

	// GDI+ font + string format
	var fontFamily, font, strFmt uintptr
	fn, _ := syscall.UTF16PtrFromString("Segoe UI")
	gpCreateFontFamily.Call(uintptr(unsafe.Pointer(fn)), 0, uintptr(unsafe.Pointer(&fontFamily)))
	if fontFamily != 0 {
		gpCreateFont.Call(fontFamily, f32(o.sTxtSz), fontStyleRegular, unitPixel, uintptr(unsafe.Pointer(&font)))
	}
	gpCreateStrFmt.Call(0, 0, uintptr(unsafe.Pointer(&strFmt)))
	if strFmt != 0 {
		gpSetStrFmtAlign.Call(strFmt, strAlignNear)
		gpSetStrFmtLineAl.Call(strFmt, strAlignCenter)
	}
	defer func() {
		if strFmt != 0 {
			gpDeleteStrFmt.Call(strFmt)
		}
		if font != 0 {
			gpDeleteFont.Call(font)
		}
		if fontFamily != 0 {
			gpDeleteFontFamily.Call(fontFamily)
		}
	}()

	// State
	const (
		fadeInSpeed  = 0.16 // per frame, ~200ms total
		fadeOutSpeed = 0.22 // per frame, ~150ms total
		fadeSlide    = 8    // px vertical slide
	)
	var (
		visible      bool
		status       Status = StatusHidden
		text         string
		barClr       uint32 = clrBlue
		volumes      [nBars]float64
		fadeProgress float64 // 0 = hidden, 1 = fully visible
		fadeTarget   float64 // 0 or 1
	)

	ticker := time.NewTicker(33 * time.Millisecond) // ~30fps
	defer ticker.Stop()

	for {
		// Drain Win32 messages
		var m wMsg
		for {
			ret, _, _ := uPeekMessage.Call(uintptr(unsafe.Pointer(&m)), 0, 0, 0, pmRemove)
			if ret == 0 {
				break
			}
			uTranslateMessage.Call(uintptr(unsafe.Pointer(&m)))
			uDispatchMessage.Call(uintptr(unsafe.Pointer(&m)))
		}

		// Process commands
		for drain := true; drain; {
			select {
			case <-o.closeCh:
				return
			case show := <-o.showCh:
				if show {
					fadeTarget = 1.0
					if !visible {
						visible = true
						uShowWindow.Call(hwnd, swShow)
					}
				} else {
					fadeTarget = 0.0
				}
			case cmd := <-o.statusCh:
				status = cmd.status
				text = cmd.text
				barClr = statusColor(cmd.status)
			case vol := <-o.volCh:
				copy(volumes[0:4], volumes[1:5])
				volumes[4] = vol
			case pos := <-o.posCh:
				o.posX, o.posY = pos[0], pos[1]
				uSetWindowPos.Call(hwnd, 0, uintptr(o.posX), uintptr(o.posY), 0, 0,
					swpNoSize|swpNoZOrder|swpNoActivate)
			default:
				drain = false
			}
		}

		select {
		case <-o.closeCh:
			return
		case <-ticker.C:
		}

		if !visible {
			continue
		}

		// Update fade progress
		if fadeProgress < fadeTarget {
			fadeProgress += fadeInSpeed
			if fadeProgress > 1.0 {
				fadeProgress = 1.0
			}
		} else if fadeProgress > fadeTarget {
			fadeProgress -= fadeOutSpeed
			if fadeProgress < 0.0 {
				fadeProgress = 0.0
			}
		}

		// Fully faded out → hide window
		if fadeProgress <= 0 && fadeTarget == 0 {
			visible = false
			uShowWindow.Call(hwnd, swHide)
			continue
		}

		// Ease-out curve for smoother feel
		eased := fadeProgress * (2 - fadeProgress)

		// Slide offset: float up on show, sink down on hide (skip during user drag)
		if !o.dragging {
			slideY := int(float64(1.0-eased) * fadeSlide)
			if slideY != 0 {
				uSetWindowPos.Call(hwnd, 0, uintptr(o.posX), uintptr(o.posY+slideY), 0, 0,
					swpNoSize|swpNoZOrder|swpNoActivate)
			} else if fadeProgress >= 1.0 {
				uSetWindowPos.Call(hwnd, 0, uintptr(o.posX), uintptr(o.posY), 0, 0,
					swpNoSize|swpNoZOrder|swpNoActivate)
			}
		}

		alpha := byte(eased * 255)

		o.animTime += 0.033
		o.animate(status, volumes)
		o.renderFrame(memDC, hwnd, screenDC, font, strFmt, barClr, text, alpha)
	}
}

// ---- Animation ----

func (o *winOverlay) animate(status Status, volumes [nBars]float64) {
	s := o.scale
	t := o.animTime
	switch status {
	case StatusLoading:
		for i := 0; i < nBars; i++ {
			pulse := math.Sin(t*2+float64(i)*0.3)*0.5 + 0.5
			o.barHeights[i] = (10 + pulse*20) * s
		}
	case StatusReady, StatusDone:
		for i := 0; i < nBars; i++ {
			o.barHeights[i] = o.barHeights[i]*0.85 + 14*s*0.15
		}
	case StatusRecording, StatusFreetalking:
		hasReal := false
		for _, v := range volumes {
			if v > 0.01 {
				hasReal = true
				break
			}
		}
		for i := 0; i < nBars; i++ {
			var target float64
			if hasReal {
				target = (8 + volumes[i]*32) * s
			} else {
				wave := math.Sin(t*4+float64(i)*0.8)*0.5 + 0.5
				rnd := math.Sin(t*7+float64(i)*1.5) * 0.3
				target = (10 + (wave+rnd)*25) * s
			}
			o.barHeights[i] = o.barHeights[i]*0.6 + target*0.4
		}
	case StatusProcessing:
		for i := 0; i < nBars; i++ {
			wave := math.Sin(t*3+float64(i)*0.6)*0.5 + 0.5
			o.barHeights[i] = (12 + wave*18) * s
		}
	default:
		for i := 0; i < nBars; i++ {
			o.barHeights[i] = o.barHeights[i]*0.85 + 14*s*0.15
		}
	}
}

// ---- Rendering ----

func (o *winOverlay) renderFrame(memDC, hwnd, screenDC, font, strFmt uintptr, barClr uint32, text string, alpha byte) {
	var gfx uintptr
	gpFromHDC.Call(memDC, uintptr(unsafe.Pointer(&gfx)))
	if gfx == 0 {
		return
	}
	defer gpDeleteGraphics.Call(gfx)

	gpSetSmoothing.Call(gfx, smoothingAntiAlias8x8)
	gpSetTextHint.Call(gfx, textHintAntiAlias)
	gpClear.Call(gfx, 0) // transparent

	// Capsule background
	fillRoundRect(gfx, 0, 0, float64(o.sW), float64(o.sH), o.sCapR, clrCapsuleBG)

	// Inner border
	strokeRoundRect(gfx, 0.5, 0.5, float64(o.sW)-1, float64(o.sH)-1, o.sCapR-0.5, clrBorder, 1)

	// Layout: bars + optional text, centered
	barsW := float32(float64(nBars)*o.sBarW + float64(nBars-1)*o.sBarGap)
	var textW float32
	if text != "" && font != 0 {
		textW = measureText(gfx, font, strFmt, text)
	}
	totalW := barsW
	if textW > 0 {
		totalW += float32(o.sTxtGap) + textW
	}
	startX := (float32(o.sW) - totalW) / 2

	// Bars
	for i := 0; i < nBars; i++ {
		bx := startX + float32(float64(i)*(o.sBarW+o.sBarGap))
		bh := float32(o.barHeights[i])
		if bh < float32(6*o.scale) {
			bh = float32(6 * o.scale)
		}
		if bh > float32(40*o.scale) {
			bh = float32(40 * o.scale)
		}
		by := (float32(o.sH) - bh) / 2
		fillRoundRect(gfx, float64(bx), float64(by), o.sBarW, float64(bh), o.sBarR, barClr)
	}

	// Text
	if text != "" && font != 0 && strFmt != 0 {
		tx := startX + barsW + float32(o.sTxtGap)
		drawText(gfx, font, strFmt, text, tx, 0, textW+4, float32(o.sH), clrText)
	}

	// UpdateLayeredWindow
	ptSrc := wPoint{0, 0}
	sz := wSize{o.sW, o.sH}
	blend := wBlend{op: acSrcOver, srcAlpha: alpha, alphaFmt: acSrcAlpha}
	uUpdateLayeredWin.Call(hwnd, screenDC, 0, uintptr(unsafe.Pointer(&sz)),
		memDC, uintptr(unsafe.Pointer(&ptSrc)), 0, uintptr(unsafe.Pointer(&blend)), ulwAlpha)
}

// ---- GDI+ drawing helpers ----

func makeRoundRectPath(x, y, w, h, r float64) uintptr {
	var path uintptr
	gpCreatePath.Call(fillModeAlternate, uintptr(unsafe.Pointer(&path)))
	if path == 0 {
		return 0
	}
	fx, fy, fw, fh, fd := float32(x), float32(y), float32(w), float32(h), float32(r*2)
	// top-left
	gpAddPathArc.Call(path, f32(fx), f32(fy), f32(fd), f32(fd), f32(180), f32(90))
	// top-right
	gpAddPathArc.Call(path, f32(fx+fw-fd), f32(fy), f32(fd), f32(fd), f32(270), f32(90))
	// bottom-right
	gpAddPathArc.Call(path, f32(fx+fw-fd), f32(fy+fh-fd), f32(fd), f32(fd), f32(0), f32(90))
	// bottom-left
	gpAddPathArc.Call(path, f32(fx), f32(fy+fh-fd), f32(fd), f32(fd), f32(90), f32(90))
	gpClosePathFigure.Call(path)
	return path
}

func fillRoundRect(gfx uintptr, x, y, w, h, r float64, color uint32) {
	path := makeRoundRectPath(x, y, w, h, r)
	if path == 0 {
		return
	}
	defer gpDeletePath.Call(path)
	var brush uintptr
	gpCreateSolidFill.Call(uintptr(color), uintptr(unsafe.Pointer(&brush)))
	if brush == 0 {
		return
	}
	defer gpDeleteBrush.Call(brush)
	gpFillPath.Call(gfx, brush, path)
}

func strokeRoundRect(gfx uintptr, x, y, w, h, r float64, color uint32, width float32) {
	path := makeRoundRectPath(x, y, w, h, r)
	if path == 0 {
		return
	}
	defer gpDeletePath.Call(path)
	var pen uintptr
	gpCreatePen.Call(uintptr(color), f32(width), unitPixel, uintptr(unsafe.Pointer(&pen)))
	if pen == 0 {
		return
	}
	defer gpDeletePen.Call(pen)
	gpDrawPath.Call(gfx, pen, path)
}

func measureText(gfx, font, strFmt uintptr, text string) float32 {
	u := utf16(text)
	layout := rectF{0, 0, 500, 100}
	var box rectF
	gpMeasureString.Call(gfx, uintptr(unsafe.Pointer(&u[0])), uintptr(len(u)),
		font, uintptr(unsafe.Pointer(&layout)), strFmt, uintptr(unsafe.Pointer(&box)), 0, 0)
	return box.w
}

func drawText(gfx, font, strFmt uintptr, text string, x, y, w, h float32, color uint32) {
	u := utf16(text)
	var brush uintptr
	gpCreateSolidFill.Call(uintptr(color), uintptr(unsafe.Pointer(&brush)))
	if brush == 0 {
		return
	}
	defer gpDeleteBrush.Call(brush)
	layout := rectF{x, y, w, h}
	gpDrawString.Call(gfx, uintptr(unsafe.Pointer(&u[0])), uintptr(len(u)),
		font, uintptr(unsafe.Pointer(&layout)), strFmt, brush)
}

func utf16(s string) []uint16 {
	r := make([]uint16, 0, len(s)+1)
	for _, c := range s {
		if c <= 0xFFFF {
			r = append(r, uint16(c))
		} else {
			c -= 0x10000
			r = append(r, uint16(c>>10)+0xD800, uint16(c&0x3FF)+0xDC00)
		}
	}
	return r
}
