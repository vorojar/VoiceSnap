//go:build !windows

package overlay

type stubOverlay struct{}

func New() Overlay                                { return &stubOverlay{} }
func (o *stubOverlay) Show()                      {}
func (o *stubOverlay) Hide()                      {}
func (o *stubOverlay) SetStatus(Status, string)   {}
func (o *stubOverlay) SetVolume(float64)          {}
func (o *stubOverlay) SetPosition(int, int)       {}
func (o *stubOverlay) GetPosition() (int, int)    { return 0, 0 }
func (o *stubOverlay) OnDragged(func(int, int))   {}
func (o *stubOverlay) Close()                     {}
