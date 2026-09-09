# Web 静态部署

当前访问入口：[https://pinggo.willer.tech/tts/](https://pinggo.willer.tech/tts/)。

Web 包不需要后台应用进程；接发评估在浏览器中分批运行。构建输出 `Builds/Web/<版本>/site/`，包含 HTML、Unity loader、压缩资源及第三方许可。

## nginx 路由

将 [tts.conf](nginx/tts.conf) 作为独立片段包含在该域名的 **HTTPS server** 内：

```nginx
include /etc/nginx/locations/tabletennis-tts.conf;
```

- `/tts` 跳转到 `/tts/`。
- HTML 不缓存；资源使用相对路径和带哈希的文件名。
- `.unityweb` 文件由 Unity 的解压回退处理，不依赖服务器设置 Content-Encoding。
- 静态目录指向 `/opt/tabletennis/current/`；该链接指向 `/opt/tabletennis/releases/<发布编号>/`。
- 片段只定义 `/tts` 路由，不替换主站、其他应用或 TLS 配置。

## 发布与回滚

1. 上传到新的版本目录，按文件 SHA-256 核对完整性；不要把源码、服务器配置或私钥放入站点目录。
2. 保留 nginx 原配置备份和 `current` 原链接目标。
3. 原子切换 `current`，首次部署时加入上面的 include。
4. `nginx -t` 通过后再 reload。
5. 检查 `/tts/` 返回 200、构建信息正确、所有 Build 资源可加载，再运行浏览器输入／接发／下载检查，并复核其他路由。
6. 失败时恢复原配置和 `current`，重新检查并 reload。保留旧版本目录，便于回滚。

`server-config.env` 使用本地 `ip`、`user` 和 `ssh_key`（密钥文件路径）字段，已被 Git 忽略。它不进入构建包，也不上传为 Web 资源。
