# ドローンシミュレーター 仕様書

Unity 2022.3 LTS + PLATEAU SDK v4.2.0.3

---

## スクリプト構成

### Assets/Scripts/

| ファイル | 役割 |
|---|---|
| `DroneController.cs` | 飛行物理・入力処理・外部入力API・速度クランプ・天候力自己適用（ForceMode.Force × windResistanceFactor）・ダウンウォッシュ発生/受信（`_allDrones` 静的リスト・`GetDownwashForceAt()`・`AllDrones`/`Rb` 公開プロパティ）・`IsInCollision` プロパティ（OnCollisionEnter/Stay/Exitで管理） |
| `CameraFollow.cs` | カメラ追従・FPV視点切り替え・Follow/Split/Overviewモード切り替え |
| `PropellerEffect.cs` | プロペラ回転アニメーション（ワールド空間） |
| `AsyncOperationExtensions.cs` | PLATEAU SDK互換修正 |
| `TitleReturnUI.cs` | ESCキーまたはコントロールパネルのボタンからタイトルへ戻る確認ダイアログ。左右矢印+Enter/クリックでYES/NO選択。ダイアログ表示中は全機をisKinematic固定。WeatherUI・HelpUIと連携してキー入力を無効化（`IsOpen` 静的プロパティ） |
| `MinimapCamera.cs` / `MinimapMarker.cs` / `MinimapToggle.cs` | ミニマップ |
| `DirectionLabels.cs` | N/S/E/W方角ラベル逆回転制御 |
| `HelpUI.cs` | Cキーでコントロールパネル表示切り替え・シーン別テキスト切り替え・パネル高さ自動計算・右上に「← Return to Title」ボタン。パネル開閉時に `DroneController.AllDrones` 全機に `PausePhysics()` を適用。`TitleReturnUI.IsOpen` ガードでダイアログ表示中のキー入力を無効化 |
| `PropellerAudio.cs` | プロペラ効果音（手続き的生成） |
| `DroneHUD.cs` / `BatterySystem.cs` | HUD表示・バッテリー管理 |

### Assets/Scripts/Weather/

| ファイル | 役割 |
|---|---|
| `WeatherLayer.cs` | 気象レイヤー抽象基底（ScriptableObject） |
| `SteadyWindLayer.cs` / `GustLayer.cs` / `ThermalLayer.cs` / `DowndraftLayer.cs` / `RainLayer.cs` / `FogLayer.cs` | 各気象レイヤー |
| `WeatherPreset.cs` | 気象プリセット |
| `WeatherSystem.cs` | 気象システム本体（力の合算・視覚制御）・`Instance` シングルトン・`GetForceAt(pos, time)` 公開（各DroneControllerが自己適用） |
| `WeatherLogger.cs` | 飛行ログCSV出力。キーボードモード: 1機ログ（従来フォーマット）/ TCPモード: `drone_id` 列付き全機一括ログ。TCPモードは `DroneNetworkController.spawnIndex` 順にソートして出力（DroneTCPClient の #番号と対応） |
| `WeatherUI.cs` | ゲーム内気象設定UI。パネル開閉時に `DroneController.AllDrones` 全機に `PausePhysics()` を適用。`TitleReturnUI.IsOpen` ガードでダイアログ表示中のキー入力を無効化 |
| `RainFollowDrone.cs` | 雨/風パーティクルのドローン追従 |
| `BuildingWeatherEffect.cs` | PLATEAU建物からThermal/Downdraftを自動生成。`ComputeForces(Vector3 pos, out float thermalY, out float downdraftY)` を公開しており、任意位置の建物気象効果を計算できる。キーボードモード: `drone` フィールドの単一機に `FixedUpdate` で直接力を適用。TCPモード: `WeatherLogger` が各ドローンの位置を渡して `ComputeForces()` を呼び出しログに記録 |

### Assets/Scripts/Network/

| ファイル | 役割 |
|---|---|
| `DroneSpec.cs` | 機体設定JSONのデータクラス |
| `DroneSpecLoader.cs` | `Resources/DroneSpecs/` から全JSONを自動ロード |
| `GameSettings.cs` | シーン間でモード・ポートを引き継ぐシングルトン |
| `TCPServer.cs` | TCP待受・接続管理（受信はバックグラウンドスレッド・Update()でFlushSend） |
| `DroneSpawner.cs` | 接続イベントを処理しドローンを生成・削除・autopilot処理 |
| `DroneNetworkController.cs` | TCPコマンドをDroneControllerに転送（autopilot中は無視） |
| `DroneStateBroadcaster.cs` | broadcastIntervalMsごとに状態JSONを送信（autopilotステータス含む） |
| `SensorModel.cs` | 検知範囲・FOV・LOS・ノイズフィルタリング・`GetNearbyPositions()` で他機位置取得 |
| `PathPlanner.cs` | 2フェーズ経路計画（Phase1: XZ 2D A*・Phase2: inflatedAlt から高度付与）・A*用/高度付与用2種の膨張ハイトマップ・LoS簡略化 |
| `AutopilotController.cs` | ウェイポイント追従・衝突継続タイマー（1.5s）・降下中入力カット・3D反発力・UTMスロット遅延対応・天候速度絞り |

### Assets/Scripts/Title/ ・ Assets/Editor/ ・ Assets/Shaders/

| ファイル | 役割 |
|---|---|
| `TitleSceneManager.cs` | タイトルUI（モード選択・ポート入力） |
| `DroneModelCreator.cs` | ドローン3Dモデル自動生成・マテリアルをアセット保存 |
| `MinimapSetup.cs` / `HelpSetup.cs` / `HUDSetup.cs` | UI自動セットアップ（各セットアップはシーン名でキーボード/TCP を自動判定） |
| `WeatherSetup.cs` / `BatterySetup.cs` | 気象・バッテリー自動セットアップ（BatterySetup はシーン名でキーボード/TCP を自動判定） |
| `TitleSetup.cs` | タイトルシーン自動生成。ScaleWithScreenSize（1920×1080）・グリッドオーバーレイ・ドローンアイコン・下部情報テキスト・コーナーブラケット装飾。Keyboard Mode / TCP Mode ボタンを左右並びで配置（各420×100） |
| `TCPSetup.cs` | TCPManagerを現シーンに追加 |
| `MinimapCircle.shader` | 円形ミニマップ用シェーダー |

---

## 操作方法（キーボードモード）

| キー | 動作 |
|---|---|
| Space | 離陸 / 着陸トグル（着陸中に押すとキャンセル） |
| W/S / A/D | 前後 / 左右移動 |
| ↑/↓ | 上昇 / 下降 |
| Q/E | 左右ヨー回転 |
| F | カメラ視点切り替え（サードパーソン ↔ FPV） |
| H | 高度維持モード トグル |
| B | バッテリー 有効/無効トグル |
| M | ミニマップ切り替え（100→150→200→非表示） |
| C | コントロールパネル 表示/非表示 |
| P | 気象パネル 表示/非表示 |
| L | フライトログ 開始/停止 |
| 1〜5 | 気象プリセット直接切り替え |
| Tab / ← → | 気象スライダー選択 / 強度調整（気象パネル表示中のみ） |
| Esc | タイトルへ戻る（確認ダイアログ） |

---

## 操作方法（TCPモード）

キーボードモードの飛行操作（Space / WASD / ↑↓ / Q・E / H）は TCP モードでは無効。

| キー | 動作 |
|---|---|
| Tab | 追従対象ドローンを順番に切り替え（気象パネル表示中はスキップ） |
| V | カメラモード切り替え（Follow → Split → Overview → Follow…） |
| F | カメラ視点切り替え（サードパーソン ↔ FPV）※Follow モード時のみ有効 |
| B | バッテリー 有効/無効トグル |
| M | ミニマップ切り替え |
| C | コントロールパネル 表示/非表示 |
| P | 気象パネル 表示/非表示 |
| L | フライトログ 開始/停止 |
| Esc | タイトルへ戻る（確認ダイアログ） |

---

## ドローン物理・衝突

| 項目 | 仕様 |
|---|---|
| 離陸 | Space で自動 10m 上昇→ホバリング維持 |
| ホバリング | 重力自動キャンセル |
| 着陸 | 降下中に地面低速接触で自動停止 |
| コライダー | `SphereCollider`（center=(0,0.05,0)・radius=0.55）・Awake で追加（RequireComponent不使用） |
| 衝突検知 | `ContinuousDynamic`（トンネリング防止） |
| 建物衝突 | 入射ベクトルを法線反射・`bounceDamping`（デフォルト0.5）倍に減衰。着陸中にバウンスしても `_landingRequested` フラグにより0.15秒後に着陸モードを自動復元。`isLanding` 中は `ApplyBounce` を呼ばない（着地後の浮き上がり防止） |
| 着地スピン減衰 | `isLanding` 中に `rb.angularVelocity *= 0.85f` を毎フレーム適用。オートパイロットのヨートルク蓄積による着地時クルクルを防止 |
| IsInCollision | `OnCollisionEnter`/`OnCollisionStay` で `true`・`OnCollisionExit` で `false`。壁への継続接触を AutopilotController に通知するためのフラグ（`IsBouncing` の 0.15s 一時フラグとは別） |
| 地面接触 | 法線Y > 0.7 かつ低速で自動着陸 |
| ゲームパッド | Xbox対応（左スティック：上昇/ヨー、右スティック：前後左右） |
| 最大水平速度 | 19 m/s（`maxHorizontalSpeed`）・加速力 `moveForce` = 15 |
| 最大垂直速度 | 6 m/s（`maxVerticalSpeed`）・加速力 `verticalSpeed` = 6（スペック適用時）/ 5（キーボードモードのデフォルト）（水平の約1/3）|

---

## カメラ（CameraFollow）

| キー | 動作 | 有効シーン |
|---|---|---|
| F | FPV ↔ サードパーソン切り替え | 両シーン |
| Tab | 追従対象ドローンを順番に切り替え（気象パネル表示中はスキップ） | TCP のみ |
| V | カメラモード切り替え（Follow → Split → Overview → Follow…） | TCP のみ |

**Follow モード（デフォルト）**
- サードパーソン: ワールド固定オフセット `(0, 5, -10)` でドローンを追従。回転に引きずられない
- FPV: Fキーで切り替え。ヨーのみ適用（ピッチ・ロール除外）
- 自動検索: `target == null` 時に1秒ごと `FindObjectOfType<DroneController>()` で追従対象を自動設定
- スナップ: ターゲット切り替わり時にカメラ位置を即座にスナップ
- Tab追従切替: `FindAllDrones()` は spawnIndex 順ソート。`CycleTarget()` は現在 target の配列位置を毎回検索してから +1（_targetIdx のズレを防止）

**Split モード**
- 接続機体数に応じて `Camera.rect` で画面を自動分割。cols: 1台=1 / 2〜4台=2 / 5〜6台=3 / 7〜8台=4。最終行が余りの場合はセル幅を拡張（ブラックスペースなし）
- 機体ごとに独立カメラを動的生成。モード終了時に破棄
- 各セル上部に帯状HUD（ScreenSpaceCamera Canvas）を動的生成。`#番号  ALT / SPD / BAT` を表示。番号は spawnIndex（接続順）準拠。フォントサイズはセル幅比例（cellPixelWidth × 0.034、13〜27px クランプ）
- ドローン接続/切断時にセル数が変化すると次フレームで自動再構築
- Splitモード中はメインHUD・ウェザーボタン・コントロールボタン・ミニマップを非表示。Pキー・Cキーでパネルを開閉しても非表示状態は維持される

**Overview モード**
- 全ドローンの AABB 中心座標を計算し、全機が収まる高度から見下ろす
- フレームごとに中心・高度を再計算（`max_dist × 2`、最低 30m 上空）

---

## 高度維持PID

デフォルトOFF・離陸時に自動ON・着陸時に自動OFF・Hキー（キーボードモードのみ）or [H]ボタン（キーボードモードのみ表示）でトグル。
↑↓キー操作中は目標高度を現在値に追従。kP=2.0 / kI=0.1 / kD=0.5（機体スペックで上書き可）。

---

## バッテリー

キーボードモード: デフォルト無効（∞）。TCPモード: デフォルト有効（有限・満タン）。Bキーでオン/オフ（有効化時に満タンリセット）。
アイドル30%〜フルスロットル100%の割合で消費。残量0でホバーフォース停止→落下。
天候ドレイン: `(windForceMagnitude / maxThrust) × 0.5` を追加消費（強風下では通常の数倍消費する場合あり）。
HUD常時表示：無効=白∞ / 50%超=白 / 20〜50%=黄 / 20%以下=赤。

---

## HUD

**Follow / Overview モード**: 画面左上。ALT（m）/ SPD（m/s）/ BAT（%）を常時表示。
追従対象（`CameraFollow.target`）に連動し、Tabキーで切り替わると自動追従（`SyncTarget()` でrb / BatterySystem / DroneController / altHoldButtonリスナーを差し替え）。
[H]ボタン：キーボードモードのみ表示（TCPモードでは非生成）。ON=蛍光水色(#14d1eb)+白テキスト / OFF=グレー+グレーテキスト。
`#N` ラベル：TCPモードのみ表示（Hボタン跡地・右上）。`spawnIndex + 1` を毎フレーム読み、切断による再採番にも即時対応。

**Split モード**: メインHUDは非表示。各分割セル上部の帯状HUDに表示（CameraFollowが動的生成）。

---

## ミニマップ

画面右下・円形（カスタムシェーダー）・ヘッディングアップ。
Mキーで 100×100 → 150×150 → 200×200 → 非表示。
N/S/E/W ラベルを縁に表示（N=大・白、S/E/W=小・白・黒アウトライン）。
Follow/Overview モード: `CameraFollow.target`（選択中の機体）を追従。target 未設定時は自動検索。
Split モード: 非表示（Split 解除時に Mキーの状態を復元）。

---

## 気象シミュレーション

レイヤー（ScriptableObject）を組み合わせてプリセットを構成。`Assets/Resources/WeatherLayers/` / `Assets/Resources/WeatherPresets/` に保存。

### プリセット

| # | 名前 | 構成 |
|---|---|---|
| 1 | Clear | Thermal + Downdraft |
| 2 | Light Wind | + 定常風 |
| 3 | Gusty | + 定常風 + ガスト |
| 4 | Rainy | + 雨 + 霧 |
| 5 | Storm | 全レイヤー |

strength 値は PlayerPrefs で永続化。SteadyWind・Gust の基本力は×0.25。

### 各レイヤーパラメータ

| レイヤー | パラメータ | デフォルト | 範囲 |
|---|---|---|---|
| WeatherLayer（基底） | `strength` | 1 | 0〜10 |
| SteadyWindLayer | `speed` | 5 | 0〜30 |
| GustLayer | `baseSpeed` | 3 | 0〜20 |
| | `gustStrength` | 8 | 0〜20 |
| | `frequency` | 1 | 0.1〜5 |
| ThermalLayer | `radius` | 20m | 1〜100 |
| | `liftStrength` | 5 | 0〜20 |
| DowndraftLayer | `radius` | 10m | 1〜100 |
| | `downdraftStrength` | 4 | 0〜20 |
| RainLayer | `intensity` | 0.5 | 0〜1 |
| | `downForce` | 1 | 0〜5 |
| FogLayer | `density` | 0.02 | 0〜0.1 |

ThermalLayer・DowndraftLayer の力は中心からの距離に応じて線形減衰（`1 - dist/radius`）。radius 外は0。

### 建物気象効果（BuildingWeatherEffect）

Start 時に全 MeshRenderer をスキャンし5m以上の建物をキャッシュ。Scene ビューで Gizmo 表示。

| 項目 | 値 |
|---|---|
| 建物判定の最低高さ | 5m（`minBuildingHeight`）|
| Thermal 基本強度 | 0.05（`thermalStrength`）|
| Downdraft 基本強度 | 0.1（`downdraftStrength`）|
| Thermal 有効条件 | ドローン Y > 建物 topY かつ distXZ < radiusXZ |
| 風下ゾーンオフセット | 建物中心 + 風向き × (radiusXZ + 8m) |
| 風下ゾーンサイズ | buildingRadius × 1.5 |
| Downdraft 有効高さ | (building.center.y − radiusXZ) 〜 (topY + 20m) |

Thermal・Downdraft の実効強度は対応する WeatherLayer の `strength` 値にスケールされる（レイヤーが非アクティブだと強度0）。

### 気象UI（WeatherCanvas）

Pキー or 右上ボタンで開閉。パネル表示中は全機をisKinematic固定（`DroneController.AllDrones` 全機に適用）。
スライダーパネル（幅300px）でレイヤーごとに強度0〜10倍をリアルタイム調整。

---

## 飛行ログ

Lキーで開始/停止。保存先: `DroneSimulator/WeatherLogs/weather_YYYYMMDD_HHMMSS.csv`。

**キーボードモード**（従来フォーマット）
記録項目: time / pos(xyz) / vel(xyz) / speed / altitude / weather_force(xyz) / thermal_y / downdraft_y / active_layers / event / col_normal(xyz) / impact_speed。

**TCPモード**（全機一括）
先頭に `drone_id` 列を追加。飛行中の全機を `spawnIndex` 順（DroneTCPClient の #番号順）にソートして毎フレーム記録。`drone_id` は Unity 内の機体名（`drone_xxxxxx`）。

---

## 飛行ログ可視化ツール

`LogVisualizer/visualizer.py`（Python 3 + Tkinter + matplotlib）。
起動: `LogVisualizer/dist/DroneLogVisualizer.app`（`Drone/DroneLogVisualizer.app` はシンボリックリンク）。
タブ: Time Series / 3D Path / Info。複数CSV色別比較・衝突赤点線マーカー。
設定（言語EN/JA・テーマ・フォルダ）は `~/.dronelogvisualizer.json` に永続化。
`drone_id` 列ありCSVを開くと機体ごとに自動分割・色別表示（ラベルは `#1` `#2`...）。スポーン順（=DroneTCPClient の #番号）に対応。キーボードモードCSVはそのまま従来通り表示。
凡例はグラフ上部に横並び表示（グラフ内に被らない）。グラフタイトルはグラフ内左上に表示。
時系列タブ: 基本2グラフ（高度・速度）。建物効果データが存在する場合のみ Thermal/Downdraft の3グラフ目を追加表示。Infoタブにスクロールバーあり。
3D経路タブ: 凡例はキューブ外側右・高度ラベルは `text2D` で独立配置・`view_init(elev=25, azim=-80)` で高度軸を垂直に近づけ。COLORS 8色（8機まで色被りなし）。
ファイルピッカー: Ctrl+クリックで個別トグル・Shift+クリック/Shift+↑↓で範囲選択・列ヘッダーでソート・「Finderで開く」ボタンあり。
matplotlib は遅延インポート（`_ensure_matplotlib()`）で起動を高速化。「ログを開く」クリック時にファイルピッカー表示前に初期化（タイトルに "Initializing..." 表示）。`MPLCONFIGDIR=~/.dronelogvisualizer_mpl` で永続化し、2回目以降はキャッシュ再ビルドが不要。CSV読み込みはメインスレッドで同期実行（macOS の GIL 競合により非メインスレッドでの matplotlib インポートが著しく低速なため）。Dockアイコン: ctypes で `NSApplication.setApplicationIconImage:` をスウィズリングし `canvas.draw()` によるアイコンリセットを完全ブロック。

---

## TCP クライアントアプリ

`TCPClient/client.py`（Python 3 + Tkinter）。
起動: `Drone/DroneTCPClient.app`（symlink）またはダブルクリック。

### マルチドローン UI

最大8機のドローンをテーブル形式で管理。

| UI要素 | 説明 |
|---|---|
| IP / Port | 全機共通の接続先 |
| ドローン行 | 機体（TypeA/B/C/D）・Goal X/Y/Z・ステータスを1行で管理 |
| ＋ドローン追加 | 行を末尾に追加（8機上限） |
| × ボタン | その行を切断・削除（飛行中は無効） |
| 全機接続 | 未接続行を並行接続 |
| 全機切断 | 全行を切断 |
| Autopilot 一斉開始 | 接続済み行に autopilot コマンドを一斉送信 |

ステータス表示: 未接続 → 接続中 → 接続済み → 計算中 → 飛行中(n/m) → 到着 / 失敗。
ログは `#1`, `#2` プレフィックスで各機のメッセージを識別。state メッセージはログ出力しない（高頻度のため）。
接続完了時にサーバーから受信した `spawnIndex` で `#N` ラベルを更新（TCP接続の到着順ズレによる番号不一致を防止）。行削除時に残行の `#` 列ラベルを即時再採番。`#` ヘッダーは中央揃え。
設定（IP・Port・ドローン行リスト）は `~/.dronetcpclient.json` に永続化。
右上「Specs」/「スペック」から全4機体の比較テーブルを表示（最高速度・重量・バッテリー・検知範囲・風耐性）。EN/JA 対応。
右上「Settings」/「設定」から言語（EN/JA）・ダークモードを切替。
ダイアログを閉じた後、スペック・設定ボタンの色が press 状態のままにならないよう `FlatButton.reset()` でノーマル色に復元。
入力欄以外をクリックするとフォーカスが外れる（設定ダイアログ内のクリックは対象外）。
8機上限到達時は「＋ドローン追加」ボタンをグレーアウトして上限テキストに変更（削除すると復活）。
機体削除時にウィンドウ縦幅が自動縮小する。
「Y=0 → 安全高度自動計算」は「＋ドローン追加」ボタンと同じ行に配置（Goal 列の真下）。
起動位置は縦方向 1/4（画面上部寄り）。8機追加時も画面内に収まるよう配慮。

**注意**: Unity Editor は **Run In Background** を有効化すること（Edit → Project Settings → Player → Resolution and Presentation）。無効だとフォーカスを失ったときにサーバーが停止する。

---

## クライアント・サーバーモード（TCP）

### シーン構成

| シーン | 役割 |
|---|---|
| `TitleScene` | モード選択（Keyboard / TCP）・ポート番号入力 |
| `NagoyaCity` | キーボード操作モード |
| `NagoyaCityTCP` | TCPサーバーモード（複数ドローン対応） |

### 通信プロトコル（JSON・改行区切り）

```
Client→Server: {"type":"connect","modelId":"Drone_TypeA"}
Server→Client: {"type":"connected","droneId":"drone_xxxxxx","spawnIndex":0,"specs":{...}}
Client→Server: {"type":"control","pitch":0.5,"roll":0.0,"yaw":0.0,"vertical":1.0,"takeoff":false,"land":false}
Client→Server: {"type":"autopilot","droneId":"drone_xxxxxx","goal":{"x":100.0,"y":0.0,"z":200.0}}
Server→Client: {"type":"autopilot_status","droneId":"...","status":"planning"}
Server→Client: {"type":"autopilot_status","droneId":"...","status":"flying","waypointCount":15}
Server→Client: {"type":"autopilot_status","droneId":"...","status":"arrived"}
Server→Client: {"type":"autopilot_status","droneId":"...","status":"failed","reason":"no_path_found"}
Server→Client: {"type":"state",...,"autopilot":{"active":true,"currentWaypoint":5,"totalWaypoints":15},...}
```

### 自律飛行（autopilot）仕様

| 項目 | 仕様 |
|---|---|
| グリッド解像度 | 2m（2D A* セル）/ ハイトマップサンプリング10m |
| 最大探索範囲 | 1000m |
| 安全マージン（水平） | A*ルーティング: **20m**（SafetyH=20・pad=2）コーナー回避 / 高度付与: **8m**（SafetyH_Alt=8・pad=1）200m上昇防止 |
| 安全マージン（垂直） | 建物上 5m |
| 最低飛行高度 | 5m |
| ハイトマップ上限 | 300m（名古屋の高層ビル ~250m に対応） |
| **巡航高度** | **30m（機体0）〜44m（機体7）を2mずつオフセット。Phase2の高度付与に使用** |
| **経路計画方式** | **2フェーズ: Phase1=XZ平面の2D A*（inflated:SafetyH=20mでコスト計算）→ Phase2=inflatedAlt（SafetyH=8m）から各WPに高度付与。A*用と高度付与用でハイトマップを分離し、横回り精度と200m上昇防止を両立** |
| **横回り優先コスト** | **ClimbPenalty=100（巡航高度を超えるビルの超過分 × 100 を追加コスト）。30m超ビルは事実上通行不可コスト** |
| **水平探索余白** | **150m（HorizPad）。横回りルートを広範囲に探索** |
| **降下ウェイポイント** | **ゴール直上を直接レイキャストして取得した実高度 + SafetyV（5m）or 巡航高度の高い方に中間WPを自動挿入** |
| **経路開始点** | **スポーン高度+10m または巡航高度の高い方（離陸後の実高度から計画し序盤の建物衝突を防ぐ）** |
| 到達判定距離 | 5m |
| ウェイポイントタイムアウト | 15秒（超過時は次のウェイポイントへスキップ） |
| **衝突継続スキップ** | **IsInCollision が 1.5秒以上継続したら次のWPへスキップ（壁へのずり落ちにも対応）** |
| **降下中衝突カット** | **IsInCollision=true かつ vertical<0 の場合は垂直入力を0に上書き（ビル擦り落ちでWPを誤通過するのを防止）** |
| **UTM出発スロット** | **spawnIndex × 3秒の遅延後に AutopilotController を起動。経路計画は全機同時・出発だけをずらして空域渋滞を防止** |
| **最終WP近傍の反発力** | **最終WP へ距離20m以内で分離力を線形減衰（20m→1.0、0m→0.0）。他機に押されて建物衝突するのを防止** |
| 経路計算 | 出発前1回のみ（飛行中の再計算なし） |
| ハイトマップ | 下向き RaycastAll で建物最高高度を取得（ドローンのコライダーは除外） |
| 簡略化 | **0.5m刻みLoSチェック**で中間ウェイポイントを削除（旧2m刻みはコーナーを見逃すため変更） |
| goal.y=0 | PathPlannerがハイトマップから安全高度を自動計算。0以外を指定した場合はその高度をそのまま使用（建物との衝突は保証しない） |
| 建物回避 | 横方向に回り込むルートを優先。横に回れない場合のみ上昇 |
| 到着後 | `RequestLand()` を自動呼び出し → 着陸 |
| 制御・水平最大入力 | 0.8（`maxHorizontalInput`）・天候超過時は `maxOpWind/windSpeed` 倍に縮小 |
| 制御・垂直最大入力 | 0.6（`maxVerticalInput`）|
| 減速開始距離 | 12m（`slowDownDistance`）|
| **ヨー制御** | **ヨーゲイン0.03。水平距離<2m または角度誤差<8° の場合はヨーなし（デッドバンド）** |

### 機体設定ファイル

`Assets/Resources/DroneSpecs/` にJSONを置くだけで自動認識。

| カテゴリ | 主なフィールド |
|---|---|
| physics | mass / maxThrust / moveForce / maxHorizontalSpeed / maxVerticalSpeed / drag / angularDrag / bounceDamping / colliderRadius / **windResistanceFactor** / **maxOperatingWindSpeedMs** |
| battery | capacitySeconds / idleConsumptionRate / fullThrottleConsumptionRate |
| altitudeHold | defaultEnabled / pidKp / pidKi / pidKd |
| sensor | detectionRangeM / detectionFOVDeg / useLineOfSight / positionNoiseSigma / velocityNoiseSigma / altitudeNoiseSigma |
| communication | broadcastIntervalMs |

同梱スペック:

| 機体 | コンセプト | 最高水平速度 | 重量 | バッテリー | 検知範囲 | windResistanceFactor | maxOperatingWindSpeedMs | ダウンウォッシュ力（自動）|
|---|---|---|---|---|---|---|---|---|
| `Drone_TypeA` | 標準 | 19m/s | 0.895kg | 46分 | 50m | 0.8（強い） | 15m/s | 1.3N |
| `Drone_TypeB` | 軽量小型 | 16m/s | 0.249kg | 39分 | 30m | 1.2（弱い） | 10m/s | 0.4N |
| `Drone_TypeC` | 大型・積載重視 | 12m/s | 3.5kg | 60分 | 60m | 0.5（最強） | 20m/s | 5.2N |
| `Drone_TypeD` | 高速特化 | 28m/s | 0.6kg | 20分 | 40m | 1.5（最弱） | 8m/s | 0.9N |

---

## 多機体対応

### スポーン配置（2×4 グリッド）

接続順に列0〜3・行0〜1 に割り当て。`spawnRoot` を中心に配置。

```
[1][2][3][4]   ← 行0（Z - 1.5m）
[5][6][7][8]   ← 行1（Z + 1.5m）
```

| パラメータ | デフォルト | Inspector フィールド |
|---|---|---|
| 列間隔 | 3m | `gridSpacingX` |
| 行間隔 | 3m | `gridSpacingZ` |
| 機体0の巡航高度 | 30m | `baseCruiseAlt` |
| 巡航高度オフセット | 2m/機 | `cruiseAltStep` |
| 最大機数 | 8 | `MaxDrones`（定数） |

### ドローン間衝突回避

**3D反発力**のみを採用（高度固定バンドは都市環境で崩れるため不採用）。

| パラメータ | 値 |
|---|---|
| 反発半径 | 15m |
| 反発ゲイン | 0.6 |
| 適用方向 | 3D（水平・垂直ともに押し出す） |

- `SensorModel.GetNearbyPositions(radius)` で他機位置を取得
- `AutopilotController.Update()` で通常制御入力に反発ベクトルを加算。反発力の合計は `hInputLimit * 0.5f` にクランプし、移動入力が完全に打ち消されないよう制限

---

## 機体スペック × 天候の影響差

### WeatherSystem の力適用

変更前（全機同一加速度）→ 変更後（質量 + 機体係数でスケール）。

```csharp
// 変更後: ForceMode.Force × windResistanceFactor
rb.AddForce(weatherForce * windResistanceFactor, ForceMode.Force);
```

TypeB（0.249kg × 1.2）は TypeA（0.895kg × 0.8）の約4〜5倍の加速度で風に流される。TypeC（3.5kg × 0.5）は最も安定。TypeD（0.6kg × 1.5）は最も風に弱い。

### 悪天候時の速度絞り（AutopilotController）

```
現在風力 > maxOperatingWindSpeedMs のとき
  maxHorizontalInput × (maxOperatingWindSpeedMs / 現在風力) に縮小
```

TypeD は 8m/s 超から絞り始め、TypeB は 10m/s 超、TypeA は 15m/s まで全速維持、TypeC は 20m/s まで全速維持。

### ダウンウォッシュ（DroneController）

飛行中の機体がプロペラ下降気流を発生させ、近接する下方機体に影響を与える。

| パラメータ | 値 |
|---|---|
| 水平影響半径 | 5m |
| 垂直影響深さ | 8m（発生源より下のみ） |
| 強度計算 | `rb.mass × 9.81 × 0.15 × Throttle × (1 - dXZ/5) × (1 - dY/8)` |
| 受ける側のスケール | `windResistanceFactor`（軽量機ほど大きく影響） |
| 発生条件 | `isAirborne=true` かつ `isLanding=false` かつ非kinematic |

`downwashForce` フィールド不要・`rb.mass` から自動計算。ホバリング時の実効値（真下中心）:

| 機体 | ダウンウォッシュ力 |
|---|---|
| TypeA（0.895kg） | 1.3N |
| TypeB（0.249kg） | 0.4N |
| TypeC（3.5kg） | 5.2N |
| TypeD（0.6kg） | 0.9N |

### 天候ドレイン（BatterySystem）

```
weatherExtra = (windForceMagnitude / maxThrust) × 0.5
drain = throttleDrain + weatherExtra
```

TypeB は maxThrust が小さいため、同じ風力でも消費量が多くなる。
