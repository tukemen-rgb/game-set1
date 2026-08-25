# Build Godot game from a description

このリポジトリは [Godogen](https://github.com/htdt/godogen)（MIT）の Godot/Claude
ランタイム構成に沿って運用する。以下は Godogen の runtime manifest を
この環境向けに書き起こしたもの。

- Keep durable project status in `README.md`: what is built, what is left, and an asset table.
- Generate visual assets with `/asset-gen`. Confirm the spend with the user before the first paid generation.
  - **注**: この環境には asset-gen ツールと API キー（GOOGLE_API_KEY /
    XAI_API_KEY / TRIPO3D_API_KEY）が無いため、アセットはプロシージャル生成で
    代替する。キーを用意したら本家の `publish.sh` でスキルを導入して差し替える。
    生成用プロンプト集は `docs/RESEARCH.md` にある。
- Read `godot.md` for engine guidance: stack, project layout, how to run, and how to capture.

## Delivery

Judge progress from the running game, never from a clean build: verify the
structural things yourself (it loads, no errors, assets present) and let what
you see drive the next iteration.

Decide from how the task is framed how to work. A task that invites
collaboration — open-ended, exploratory, phrased as a direction rather than a
spec — gets the live game early: checkpoint at decisions of taste, scope, or
cost, and build freely in between. A task handed over as a finished brief to
execute gets reasonable calls and steady progress, no blocking. Either way the
result is proven, not claimed — if the user hasn't seen it running, finish
with a 15–20s video of the game in action, and watch it back before you call
the work done.
