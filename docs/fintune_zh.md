# 微调

## 安装训练环境

```
pip install git+https://github.com/modelscope/FunASR
```

## 数据准备

数据格式需要包括如下几个字段：

```
{"messages": [{"role": "system", "content": "You are a helpful assistant."}, {"role": "user", "content": "语音转写：<|startofspeech|>!https://isv-data.oss-cn-hangzhou.aliyuncs.com/ics/MaaS/ASR/test_audio/BAC009S0764W0121.wav<|endofspeech|>"}, {"role": "assistant", "content": "甚至出现交易几乎停滞的情况"}], "speech_length": 418, "text_length": 6}
{"messages": [{"role": "system", "content": "You are a helpful assistant."}, {"role": "user", "content": "语音转写：<|startofspeech|>!https://isv-data.oss-cn-hangzhou.aliyuncs.com/ics/MaaS/ASR/test_audio/BAC009S0916W0489.wav<|endofspeech|>"}, {"role": "assistant", "content": "湖北一公司以员工名义贷款数十员工负债千万"}], "speech_length": 572, "text_length": 11}
```

详细可以参考：`data/train_example.jsonl`

数据准备细节介绍：

- `messages[1]["content"]`: 音频文件的路径 + 语音识别的 prompt
- `messages[2]["content"]`: 音频文件标注文本
- `speech_length`: 音频文件的 fbank 帧数
- `text_length`: 音频文件标注文本的 token 数 (用 `Qwen3-0.6B` 编码)

`train_text.txt`

左边为数据唯一 ID，需与 `train_wav.scp` 中的 ID 一一对应 右边为音频文件标注文本，格式如下：

```
BAC009S0764W0121 甚至出现交易几乎停滞的情况
BAC009S0916W0489 湖北一公司以员工名义贷款数十员工负债千万
```

`train_wav.scp`

左边为数据唯一 ID，需与 `train_text.txt` 中的 ID 一一对应 右边为音频文件的路径，格式如下

```
BAC009S0764W0121 https://isv-data.oss-cn-hangzhou.aliyuncs.com/ics/MaaS/ASR/test_audio/BAC009S0764W0121.wav
BAC009S0916W0489 https://isv-data.oss-cn-hangzhou.aliyuncs.com/ics/MaaS/ASR/test_audio/BAC009S0916W0489.wav
```

`生成指令`

```
python tools/scp2jsonl.py \
  --scp-file /path/to/train_wav.scp \
  --transcript-file /path/to/train_text.txt \
  --jsonl-file data/train_example.jsonl
```

## 启动训练

修改 `finetune.sh` 中的 `audio_encoder_conf.freeze`, `audio_adaptor_conf.freeze` 和 `llm_conf.freeze`。

将需要微调的模块 `freeze` 设置成 `false`（默认只微调 llm）。

更多参数细节参考：[SenseVoice 模型训练与测试](https://github.com/modelscope/FunASR/blob/main/docs/tutorial/README_zh.md#%E6%A8%A1%E5%9E%8B%E8%AE%AD%E7%BB%83%E4%B8%8E%E6%B5%8B%E8%AF%95)

```
bash finetune.sh
```
