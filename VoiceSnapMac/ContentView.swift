import SwiftUI

struct ContentView: View {
    @EnvironmentObject var appState: AppState
    
    var body: some View {
        VStack(spacing: 0) {
            // 自定义标题栏区域
            HStack {
                Image(systemName: "mic.circle.fill")
                    .font(.title2)
                    .foregroundColor(.white)
                Text("VoiceSnap")
                    .font(.headline)
                    .foregroundColor(.white)
                Spacer()
                
                Button(action: {
                    // 打开设置
                }) {
                    Image(systemName: "gearshape.fill")
                        .foregroundColor(.white)
                }
                .buttonStyle(.plain)
            }
            .padding()
            .background(Color(hex: "3F51B5"))
            
            // 主内容区域
            VStack(spacing: 20) {
                Spacer()
                
                // 状态指示
                VStack(spacing: 10) {
                    Image(systemName: appState.isRecording ? "waveform.circle.fill" : "mic.circle")
                        .resizable()
                        .aspectRatio(contentMode: .fit)
                        .frame(width: 80, height: 80)
                        .foregroundColor(appState.isRecording ? .red : Color(hex: "3F51B5"))
                        .symbolEffect(.pulse, isActive: appState.isRecording) // iOS 17/macOS 14+ 动画
                    
                    Text(appState.statusMessage)
                        .font(.title2)
                        .foregroundColor(.secondary)
                }
                
                // 最近识别结果
                if !appState.recognizedText.isEmpty {
                    VStack(alignment: .leading) {
                        Text("最近识别:")
                            .font(.caption)
                            .foregroundColor(.gray)
                        Text(appState.recognizedText)
                            .padding()
                            .background(Color.gray.opacity(0.1))
                            .cornerRadius(8)
                    }
                    .padding(.horizontal)
                }
                
                Spacer()
                
                // 底部提示
                Text("长按 Ctrl 键开始说话，松开结束")
                    .font(.caption)
                    .foregroundColor(.gray)
                    .padding(.bottom)
            }
            .background(Color.white)
        }
        .frame(minWidth: 400, minHeight: 500)
    }
}

#Preview {
    ContentView()
        .environmentObject(AppState())
}
