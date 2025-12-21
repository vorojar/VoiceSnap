"""
VoiceSnap Python 后端服务
提供 ASR 识别、录音等功能的 HTTP API
"""

import os
import sys
import time
import queue
import threading
from typing import Optional

# 获取脚本所在目录，添加到 Python 路径
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, SCRIPT_DIR)

# 尝试导入 model.py 以注册 FunASRNano 类
try:
    import model  # 这会注册 FunASRNano 到 funasr
    print("[ASR] 成功导入 model.py")
except ImportError as e:
    print(f"[ASR] 警告: 无法导入 model.py: {e}")

import numpy as np
import sounddevice as sd
import soundfile as sf
import torch
from funasr import AutoModel
from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse
import uvicorn

app = FastAPI(title="VoiceSnap ASR Service")

# 全局状态
class ASRState:
    def __init__(self):
        self.model: Optional[AutoModel] = None
        self.model_loaded = False
        self.is_recording = False
        self.audio_buffer: list = []
        self.stream: Optional[sd.InputStream] = None
        self.current_volume = 0.0
        self.sample_rate = 16000
        self.volume_lock = threading.Lock()

state = ASRState()


def load_model():
    """加载 ASR 模型"""
    print("[ASR] 正在加载模型...")
    
    try:
        # 获取脚本所在目录作为根目录
        script_dir = os.path.dirname(os.path.abspath(__file__))
        
        # 模型目录在脚本同级目录的 models 下
        model_dir = os.path.join(script_dir, 'models', 'Fun-ASR-Nano-2512')
        remote_code = os.path.join(script_dir, 'model.py')
        
        print(f"[ASR] 脚本目录: {script_dir}")
        print(f"[ASR] 模型目录: {model_dir}")
        print(f"[ASR] 模型代码: {remote_code}")
        
        if not os.path.exists(model_dir):
            print(f"[ASR] 错误: 模型目录不存在 {model_dir}")
            return False
        
        if not os.path.exists(remote_code):
            print(f"[ASR] 错误: 模型代码不存在 {remote_code}")
            return False
        
        # 切换到脚本目录（确保相对路径正确）
        os.chdir(script_dir)
        
        device = "cuda:0" if torch.cuda.is_available() else "cpu"
        print(f"[ASR] 使用设备: {device}")
        
        state.model = AutoModel(
            model=model_dir,
            trust_remote_code=True,
            vad_model="fsmn-vad",
            vad_kwargs={"max_single_segment_time": 30000},
            remote_code=remote_code,
            device=device,
        )
        
        state.model_loaded = True
        print(f"[ASR] 模型加载成功！")
        print("[ASR] Backend ready")
        return True
        
    except Exception as e:
        print(f"[ASR] 模型加载失败: {e}")
        import traceback
        traceback.print_exc()
        return False


@app.get("/health")
async def health_check():
    """健康检查"""
    return {"status": "ok", "model_loaded": state.model_loaded}


@app.post("/start_recording")
async def start_recording():
    """开始录音"""
    if state.is_recording:
        return JSONResponse({"error": "Already recording"}, status_code=400)
    
    state.is_recording = True
    state.audio_buffer = []
    
    def audio_callback(indata, frames, time_info, status):
        if status:
            print(f"[Audio] {status}")
        state.audio_buffer.append(indata.copy())
        
        # 计算 RMS 音量
        rms = np.sqrt(np.mean(indata ** 2))
        volume = min(1.0, rms * 10)
        
        with state.volume_lock:
            state.current_volume = volume
    
    try:
        state.stream = sd.InputStream(
            samplerate=state.sample_rate,
            channels=1,
            callback=audio_callback,
            dtype=np.float32
        )
        state.stream.start()
        print("[ASR] 开始录音")
        return {"status": "recording"}
    except Exception as e:
        state.is_recording = False
        return JSONResponse({"error": str(e)}, status_code=500)


@app.get("/volume")
async def get_volume():
    """获取当前音量"""
    with state.volume_lock:
        return {"volume": state.current_volume}


@app.post("/stop_recording")
async def stop_recording():
    """停止录音并进行识别"""
    if not state.is_recording:
        return JSONResponse({"error": "Not recording"}, status_code=400)
    
    state.is_recording = False
    
    # 停止录音流
    if state.stream:
        state.stream.stop()
        state.stream.close()
        state.stream = None
    
    print("[ASR] 录音结束，正在识别...")
    
    # 处理音频
    if len(state.audio_buffer) == 0:
        return {"text": "", "success": False, "error": "No audio data"}
    
    try:
        audio_array = np.concatenate(state.audio_buffer, axis=0)
        
        # 识别
        if state.model and state.model_loaded:
            # 直接传入 numpy 数组
            res = state.model.generate(input=audio_array, cache={}, batch_size=1)
            text = res[0]["text"].strip()
        else:
            # 模拟模式
            text = "[模拟模式] 这是一段测试文本"
        
        print(f"[ASR] 识别结果: {text}")
        return {"text": text, "success": True}
        
    except Exception as e:
        print(f"[ASR] 识别失败: {e}")
        return {"text": "", "success": False, "error": str(e)}


@app.post("/recognize")
async def recognize_audio(request: Request):
    """接收音频数据并进行识别 (C# 客户端发送的音频)"""
    try:
        import io
        # 读取音频数据
        audio_bytes = await request.body()
        print(f"[ASR] 收到音频数据: {len(audio_bytes)} bytes")
        
        if len(audio_bytes) < 1000:
            return {"text": "", "success": False, "error": "Audio data too short"}
        
        # 直接从内存读取音频数据
        with io.BytesIO(audio_bytes) as bio:
            audio_array, samplerate = sf.read(bio)
        
        # 识别
        if state.model and state.model_loaded:
            # FunASR 支持直接传入 numpy 数组
            res = state.model.generate(input=audio_array, cache={}, batch_size=1)
            text = res[0]["text"].strip()
        else:
            # 模拟模式
            text = "[模拟模式] 这是一段测试文本"
        
        print(f"[ASR] 识别结果: {text}")
        return {"text": text, "success": True}
        
    except Exception as e:
        print(f"[ASR] 识别失败: {e}")
        import traceback
        traceback.print_exc()
        return {"text": "", "success": False, "error": str(e)}


@app.on_event("startup")
async def startup_event():
    """应用启动时加载模型"""
    # 在后台线程加载模型
    threading.Thread(target=load_model, daemon=True).start()


if __name__ == "__main__":
    print("[ASR] VoiceSnap Python 后端服务启动中...")
    uvicorn.run(app, host="127.0.0.1", port=8765, log_level="info")
