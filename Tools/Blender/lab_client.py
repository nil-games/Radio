"""One-shot stdio MCP client for the locally installed Blender Lab server."""
import asyncio
import base64
import json
import os
import sys
from pathlib import Path
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

async def main():
    env = dict(os.environ, BLENDER_PATH=r'F:\SteamLibrary\steamapps\common\Blender\blender.exe')
    params = StdioServerParameters(command=sys.executable, args=['-m', 'blmcp'], env=env)
    async with stdio_client(params) as (rd, wr):
        async with ClientSession(rd, wr) as session:
            await session.initialize()
            if len(sys.argv) == 1:
                value = await session.list_tools()
            else:
                args = ({'code': Path(sys.argv[2]).read_text(encoding='utf-8')} if sys.argv[2].endswith('.py') else json.loads(Path(sys.argv[2]).read_text(encoding='utf-8'))) if len(sys.argv)>2 else {}
                value = await session.call_tool(sys.argv[1], args)
            for c in getattr(value, 'content', []):
                if c.type == 'image':
                    path = Path(__file__).parent / 'cat_mcp_capture.png'
                    path.write_bytes(base64.b64decode(c.data))
                    c.data = '[saved to ' + str(path) + ']'
            print(value.model_dump_json())

asyncio.run(main())
