import Cocoa

class InputSimulator {
    static func simulateString(_ string: String) {
        let source = CGEventSource(stateID: .hidSystemState)
        
        for char in string {
            // 将字符转换为 UTF-16
            let scalars = String(char).unicodeScalars
            let value = scalars[scalars.startIndex].value
            
            // 创建按键按下事件
            guard let keyDown = CGEvent(keyboardEventSource: source, virtualKey: 0, keyDown: true) else { continue }
            keyDown.keyboardSetUnicodeString(stringLength: 1, unicodeString: [UniChar(value)])
            keyDown.post(tap: .cghidEventTap)
            
            // 创建按键释放事件
            guard let keyUp = CGEvent(keyboardEventSource: source, virtualKey: 0, keyDown: false) else { continue }
            keyUp.keyboardSetUnicodeString(stringLength: 1, unicodeString: [UniChar(value)])
            keyUp.post(tap: .cghidEventTap)
        }
    }
}
