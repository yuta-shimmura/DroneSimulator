using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 2フェーズ経路計画
//   Phase 1: XZ平面の2D A*。建物を「高さコスト」として扱い横回り経路を求める
//   Phase 2: 各2Dウェイポイントに必要最低限の飛行高度を付与
// → 建物を越えるより横に回り込む方が原則安くなり、都市部でも水平回避が保証される
public static class PathPlanner
{
    const float VoxelSize        = 2f;    // 2D グリッド解像度
    const float HmapStep         = 10f;   // ハイトマップサンプリング間隔
    const float HorizPad         = 150f;  // 探索領域の水平余白（広げて横回りルートを確保）
    const float MaxAltitude      = 300f;  // ハイトマップ上限
    const float SafetyH          = 20f;   // 建物水平膨張マージン（A*ルーティング用、pad=2→実効20m）
    const float SafetyH_Alt      = 8f;    // 高度付与用マージン（pad=1→実効10m、200m上昇防止）
    const float SafetyV          = 5f;    // 建物垂直クリアランス
    const float MinAlt           = 5f;    // 最低飛行高度
    const float MaxRange         = 1000f;
    const float DefaultCruiseAlt = 30f;

    // 巡航高度を超えるビル上空を通るときの追加コスト（/m）
    // 100 → 30m超過ビルのセル通過コスト≒3001、横回り3000m相当。事実上の通行不可
    const float ClimbPenalty = 100f;

    public static IEnumerator PlanAsync(Vector3 start, Vector3 goal,
                                        Action<List<Vector3>> callback,
                                        float cruiseAlt = DefaultCruiseAlt)
    {
        float dist = Vector3.Distance(start, goal);
        if (dist > MaxRange)
        {
            Debug.LogWarning($"[PathPlanner] Goal exceeds max range: {dist:F0}m > {MaxRange}m.");
            callback(null);
            yield break;
        }

        float minX = Mathf.Min(start.x, goal.x) - HorizPad;
        float minZ = Mathf.Min(start.z, goal.z) - HorizPad;
        float maxX = Mathf.Max(start.x, goal.x) + HorizPad;
        float maxZ = Mathf.Max(start.z, goal.z) + HorizPad;

        int nx  = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) / VoxelSize));
        int nz  = Mathf.Max(1, Mathf.CeilToInt((maxZ - minZ) / VoxelSize));
        int hmx = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) / HmapStep));
        int hmz = Mathf.Max(1, Mathf.CeilToInt((maxZ - minZ) / HmapStep));

        var origin = new Vector3(minX, 0f, minZ);

        // ── Phase 0: ハイトマップ構築（20回ごとに yield）────────────────
        var   hmapRaw = new float[hmx, hmz];
        float rayY    = MaxAltitude + 10f;
        float rayLen  = MaxAltitude + 20f;
        int   rayCnt  = 0;

        for (int ix = 0; ix < hmx; ix++)
        for (int iz = 0; iz < hmz; iz++)
        {
            float wx = origin.x + (ix + 0.5f) * HmapStep;
            float wz = origin.z + (iz + 0.5f) * HmapStep;
            var   ro = new Vector3(wx, rayY, wz);
            var allHits = Physics.RaycastAll(ro, Vector3.down, rayLen,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float topY = 0f;
            foreach (var h in allHits)
                if (h.collider.GetComponentInParent<DroneController>() == null)
                    topY = Mathf.Max(topY, h.point.y);
            hmapRaw[ix, iz] = topY;

            if (++rayCnt % 20 == 0) yield return null;
        }

        // A*ルーティング用（SafetyH=20m）と高度付与用（SafetyH_Alt=8m）の2種のハイトマップ
        float[,] inflated    = InflateHeightMap(hmapRaw, hmx, hmz, nx, nz, SafetyH);
        float[,] inflatedAlt = InflateHeightMap(hmapRaw, hmx, hmz, nx, nz, SafetyH_Alt);

        // ── ローカル関数 ──────────────────────────────────────────────
        Vector2Int WorldToCell(float wx, float wz) => new Vector2Int(
            Mathf.Clamp(Mathf.FloorToInt((wx - origin.x) / VoxelSize), 0, nx - 1),
            Mathf.Clamp(Mathf.FloorToInt((wz - origin.z) / VoxelSize), 0, nz - 1));

        Vector3 CellToWorld(Vector2Int c, float y) => new Vector3(
            origin.x + (c.x + 0.5f) * VoxelSize,
            y,
            origin.z + (c.y + 0.5f) * VoxelSize);


        // ── Phase 1: 2D A*（XZ平面）────────────────────────────────────
        // コスト = 移動距離 + 巡航高度を超えるビルの超過分 × ClimbPenalty
        // 横回りの方が原則安くなる。どうしても越えられない場合のみ上昇を許容
        var startC = WorldToCell(start.x, start.z);
        var goalC  = WorldToCell(goal.x,  goal.z);

        var gCost  = new Dictionary<Vector2Int, float>();
        var parent = new Dictionary<Vector2Int, Vector2Int>();
        var closed = new HashSet<Vector2Int>();
        var open   = new MinHeap2D();

        gCost[startC] = 0f;
        open.Push(startC, Heur2D(startC, goalC));

        bool      found     = false;
        Vector2Int finalCell = startC;
        int        iter      = 0;

        while (open.Count > 0)
        {
            var cur = open.Pop();
            if (closed.Contains(cur)) continue;
            closed.Add(cur);

            if (cur == goalC) { finalCell = cur; found = true; break; }

            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dz == 0) continue;
                var nb = new Vector2Int(cur.x + dx, cur.y + dz);
                if (nb.x < 0 || nb.x >= nx || nb.y < 0 || nb.y >= nz) continue;
                if (closed.Contains(nb)) continue;

                float overShoot = Mathf.Max(0f, inflated[nb.x, nb.y] - cruiseAlt);
                float moveDist  = (dx != 0 && dz != 0) ? 1.41421f : 1f;
                float g = gCost[cur] + moveDist + overShoot * ClimbPenalty;

                if (!gCost.TryGetValue(nb, out float eg) || g < eg)
                {
                    gCost[nb]  = g;
                    parent[nb] = cur;
                    open.Push(nb, g + Heur2D(nb, goalC));
                }
            }

            if (++iter % 500 == 0) yield return null;
        }

        if (!found)
        {
            Debug.LogWarning("[PathPlanner] No path found.");
            callback(null);
            yield break;
        }

        // ── Phase 2: 2D経路に高度を付与して3Dウェイポイント列を生成 ──────
        var cells = new List<Vector2Int>();
        var c2    = finalCell;
        while (parent.ContainsKey(c2)) { cells.Add(c2); c2 = parent[c2]; }
        cells.Add(startC);
        cells.Reverse();

        var raw = new List<Vector3>();
        foreach (var c in cells)
            raw.Add(CellToWorld(c, Mathf.Max(inflatedAlt[c.x, c.y], Mathf.Max(MinAlt, cruiseAlt))));

        // 始点・終点は実際の高度で上書き
        raw[0]             = new Vector3(raw[0].x, start.y, raw[0].z);
        raw[raw.Count - 1] = goal;

        // ── 視線チェックで冗長ウェイポイントを間引く ─────────────────────
        var path = SimplifyPath(raw, inflated, origin, nx, nz);

        // ゴール直上を直接レイキャストして真の地上高を取得（グリッド近似誤差を排除）
        float goalGroundH = 0f;
        var   goalRayO    = new Vector3(goal.x, MaxAltitude + 10f, goal.z);
        foreach (var h in Physics.RaycastAll(goalRayO, Vector3.down, MaxAltitude + 20f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.GetComponentInParent<DroneController>() == null)
                goalGroundH = Mathf.Max(goalGroundH, h.point.y);
        }
        float descentAlt = Mathf.Max(goalGroundH + SafetyV, cruiseAlt);
        if (goal.y < descentAlt - VoxelSize)
        {
            path.Insert(path.Count - 1, new Vector3(goal.x, descentAlt, goal.z));
            Debug.Log($"[PathPlanner] Inserted descent waypoint at alt={descentAlt:F1}m.");
        }

        Debug.Log($"[PathPlanner] Path found: {path.Count} waypoints (raw={raw.Count}).");
        callback(path);
    }

    // ── ハイトマップ膨張（建物に安全マージンを付加）──────────────────────
    static float[,] InflateHeightMap(float[,] raw, int hmx, int hmz, int nx, int nz, float safetyH)
    {
        int pad  = Mathf.CeilToInt(safetyH / HmapStep);
        var out_ = new float[nx, nz];

        for (int ix = 0; ix < nx; ix++)
        for (int iz = 0; iz < nz; iz++)
        {
            int hix = Mathf.Clamp(Mathf.FloorToInt(ix * VoxelSize / HmapStep), 0, hmx - 1);
            int hiz = Mathf.Clamp(Mathf.FloorToInt(iz * VoxelSize / HmapStep), 0, hmz - 1);

            float maxH = 0f;
            for (int dx = -pad; dx <= pad; dx++)
            for (int dz = -pad; dz <= pad; dz++)
            {
                int nx2 = hix + dx, nz2 = hiz + dz;
                if (nx2 >= 0 && nx2 < hmx && nz2 >= 0 && nz2 < hmz)
                    maxH = Mathf.Max(maxH, raw[nx2, nz2]);
            }
            out_[ix, iz] = maxH + SafetyV;
        }
        return out_;
    }

    // ── 経路簡略化（視線チェック）────────────────────────────────────
    static List<Vector3> SimplifyPath(List<Vector3> path, float[,] inflated,
                                      Vector3 origin, int nx, int nz)
    {
        if (path.Count <= 2) return path;
        var result = new List<Vector3> { path[0] };
        int i = 0;
        while (i < path.Count - 1)
        {
            int j = path.Count - 1;
            while (j > i + 1 && !LoS(path[i], path[j], inflated, origin, nx, nz))
                j--;
            result.Add(path[j]);
            i = j;
        }
        return result;
    }

    static bool LoS(Vector3 a, Vector3 b, float[,] inflated, Vector3 origin, int nx, int nz)
    {
        // 0.5m 刻みでサンプリング（2m だとビルコーナーを見逃すため細かく）
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 0.5f));
        for (int k = 1; k < steps; k++)
        {
            var p  = Vector3.Lerp(a, b, (float)k / steps);
            int ix = Mathf.FloorToInt((p.x - origin.x) / VoxelSize);
            int iz = Mathf.FloorToInt((p.z - origin.z) / VoxelSize);
            if (ix < 0 || ix >= nx || iz < 0 || iz >= nz) continue;
            if (p.y < MinAlt || p.y <= inflated[ix, iz]) return false;
        }
        return true;
    }

    // ── ユーティリティ ───────────────────────────────────────────────
    static float Heur2D(Vector2Int a, Vector2Int b)
    {
        int dx = a.x - b.x, dz = a.y - b.y;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // ── 2D 最小ヒープ ────────────────────────────────────────────────
    class MinHeap2D
    {
        readonly List<(float f, Vector2Int v)> _h = new List<(float, Vector2Int)>();
        public int Count => _h.Count;

        public void Push(Vector2Int v, float f)
        {
            _h.Add((f, v));
            int i = _h.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (_h[p].f <= _h[i].f) break;
                Swap(i, p);
                i = p;
            }
        }

        public Vector2Int Pop()
        {
            var top  = _h[0].v;
            int last = _h.Count - 1;
            _h[0] = _h[last];
            _h.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = 2 * i + 1, r = 2 * i + 2, s = i;
                if (l < _h.Count && _h[l].f < _h[s].f) s = l;
                if (r < _h.Count && _h[r].f < _h[s].f) s = r;
                if (s == i) break;
                Swap(i, s);
                i = s;
            }
            return top;
        }

        void Swap(int a, int b) => (_h[a], _h[b]) = (_h[b], _h[a]);
    }
}
