# InteriorDesignWeb

AI 室内设计工作台：图库检索、方案与房间管理、ComfyUI 生图/编辑/视频任务、AI 设计助手和管理员后台。

## 开发接续入口

后续开发者或 AI 请按以下顺序阅读：

1. [`PROJECT_HANDOFF.md`](PROJECT_HANDOFF.md)：当前项目结构、模块链路、完成度、风险和接续规则；
2. [`DEVELOPMENT_ROADMAP.md`](DEVELOPMENT_ROADMAP.md)：任务优先级和产品排期；
3. [`PROJECT_MEMORY.md`](PROJECT_MEMORY.md)：历史问题、设计决策和已完成修复；
4. [`ASSISTANT_DIAGNOSTICS.md`](ASSISTANT_DIAGNOSTICS.md)：AI 助手链路和日志定位；
5. [`DEPLOYMENT.md`](DEPLOYMENT.md)：Linux、Nginx、systemd 和发布流程；
6. [`SECURITY_SETUP.md`](SECURITY_SETUP.md)：密钥、JWT 和生产配置规则。

## 主要目录

```text
InteriorDesignWeb/               ASP.NET Core 8 后端
InteriorDesignWeb/wwwroot/src/   React 19 + TypeScript 前端源码
InteriorDesignWeb/workflow/      7 个 ComfyUI API 工作流 JSON
InteriorDesignWeb/database/      MySQL 迁移脚本和迁移清单
InteriorDesignWeb.Tests/         xUnit 策略与兼容性测试
scripts/                         数据库迁移和发布包检查脚本
```

生产发布统一使用 `dotnet publish`；项目会自动构建前端并只发布 `wwwroot/dist`。
