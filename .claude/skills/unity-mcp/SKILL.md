---
name: unity-mcp
description: 실행 중인 Unity 에디터를 MCP로 직접 조작하는 법과 그 함정. MCP 도구가 0개로 보일 때, 어느 체크아웃의 에디터에 붙는지 확인해야 할 때, 워크트리에 상주 헤드리스 에디터를 띄울 때, Pipeline 패키지나 unity-cli 스킬 사본을 갱신할 때 읽는다. AGENTS.md 규칙 15·17과 겹치는 자리를 다룬다.
---

# Unity MCP

본문은 [`docs/agents/unity-mcp.md`](../../../docs/agents/unity-mcp.md)에 있다. **지금 읽어라.**

이 파일은 Claude Code가 스킬을 찾게 하는 얇은 껍데기다. 내용을 여기 복사하지 않는다 — 저장소를
Codex·Cursor·Gemini CLI 등으로 여는 세션은 `docs/agents/`를 직접 읽으므로, 원본이 둘이 되면
한쪽이 조용히 썩는다.

`.claude/skills/unity-cli/`는 이것과 성격이 다르다. 그쪽은 포인터가 아니라 Unity CLI가 자기 문서를
내보낸 **벤더링 사본**이며, 손으로 고치지 않고 `unity skill refresh`로 다시 렌더한다.
