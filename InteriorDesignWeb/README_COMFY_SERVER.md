# ComfyUI Server 单链路版本

后端只保留以下执行链路：

```text
前端 -> ASP.NET Core -> ComfyUI Server -> Partner Nodes/本地节点 -> COS -> 数据库
```

## 必填配置

```json
"ComfyUI": {
  "ApiUrl": "http://你的ComfyUI服务器IP:8188/",
  "AccountApiKey": "通过环境变量提供",
  "AuthorizationHeader": "",
  "UseProxy": false
}
```

推荐环境变量：

```powershell
$env:ComfyUI__AccountApiKey="comfyui-你的Key"
```

如果 ComfyUI 前方有 Nginx Bearer 鉴权：

```powershell
$env:ComfyUI__AuthorizationHeader="Bearer 你的服务器入口Token"
```

详细说明见：`docs/ComfyUI服务器接入说明.md`。
