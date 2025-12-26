import Cocoa
import Carbon

class HotkeyService {
    var onKeyDown: (() -> Void)?
    var onKeyUp: (() -> Void)?
    
    private var eventTap: CFMachPort?
    private var runLoopSource: CFRunLoopSource?
    
    init() {
        startMonitoring()
    }
    
    func startMonitoring() {
        // 监听 Ctrl 键 (FlagsChanged)
        let eventMask = (1 << CGEventType.flagsChanged.rawValue)
        
        guard let eventTap = CGEvent.tapCreate(
            tap: .cgSessionEventTap,
            place: .headInsertEventTap,
            options: .defaultTap,
            eventsOfInterest: CGEventMask(eventMask),
            callback: { (proxy, type, event, refcon) -> Unmanaged<CGEvent>? in
                let service = Unmanaged<HotkeyService>.fromOpaque(refcon!).takeUnretainedValue()
                service.handleEvent(event: event)
                return Unmanaged.passUnretained(event)
            },
            userInfo: Unmanaged.passUnretained(self).toOpaque()
        ) else {
            print("Failed to create event tap. Check Accessibility permissions.")
            return
        }
        
        self.eventTap = eventTap
        self.runLoopSource = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, eventTap, 0)
        CFRunLoopAddSource(CFRunLoopGetCurrent(), runLoopSource, .commonModes)
        CGEvent.tapEnable(tap: eventTap, true)
    }
    
    private var isCtrlPressed = false
    
    private func handleEvent(event: CGEvent) {
        let flags = event.flags
        // 检查 Ctrl 键 (maskControl)
        let ctrlPressed = flags.contains(.maskControl)
        
        if ctrlPressed && !isCtrlPressed {
            isCtrlPressed = true
            DispatchQueue.main.async {
                self.onKeyDown?()
            }
        } else if !ctrlPressed && isCtrlPressed {
            isCtrlPressed = false
            DispatchQueue.main.async {
                self.onKeyUp?()
            }
        }
    }
    
    deinit {
        if let runLoopSource = runLoopSource {
            CFRunLoopRemoveSource(CFRunLoopGetCurrent(), runLoopSource, .commonModes)
        }
    }
}
