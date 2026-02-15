package overlay

// Status represents the indicator display state.
type Status string

const (
	StatusHidden     Status = "hidden"
	StatusLoading    Status = "loading"
	StatusReady      Status = "ready"
	StatusRecording    Status = "recording"
	StatusFreetalking  Status = "freetalking"
	StatusProcessing Status = "processing"
	StatusDone       Status = "done"
	StatusCancelled  Status = "cancelled"
	StatusNoVoice    Status = "no_voice"
	StatusNoContent  Status = "no_content"
	StatusError      Status = "error"
)

// Overlay is a native floating indicator window with per-pixel alpha.
type Overlay interface {
	Show()
	Hide()
	SetStatus(status Status, text string)
	SetVolume(vol float64)
	SetPosition(x, y int)
	GetPosition() (int, int)
	OnDragged(fn func(x, y int))
	Close()
}
