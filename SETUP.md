# ドローンシミュレーター セットアップ手順

Unity 2022.3 LTS + PLATEAU SDK v4.2.0.3 + 名古屋市データ

---

## STEP 1〜3: Unity Hub・エディタ・プロジェクト

1. [unity.com/download](https://unity.com/download) から Unity Hub をインストール・サインイン
2. Unity Hub「Installs」→「Install Editor」→ Unity 2022.3 LTS を選択（Mac Build Support にチェック）
3. Unity Hub「Projects」→「New Project」→ テンプレート: **Universal 3D（URP）**・プロジェクト名: `DroneSimulator`

---

## STEP 4: Input System パッケージの導入

Window → Package Manager →「+」→「Add package by name」→ `com.unity.inputsystem` を入力して Add（再起動ダイアログが出たら「Yes」）

---

## STEP 5: PLATEAU SDK for Unity の導入

1. [PLATEAU-SDK-for-Unity Releases](https://github.com/Project-PLATEAU/PLATEAU-SDK-for-Unity) から最新 `.tgz` をダウンロード
   > Mac は Safari の「ダウンロード後にファイルを開く」を外してから再ダウンロード
2. Window → Package Manager →「+」→「Add package from tarball」→ `.tgz` を選択

**トラブルシューティング**

| エラー | 対処 |
|---|---|
| `error CS1061 'AsyncOperation' GetAwaiter` | `com.unity.addressables` をインストール後、`AddressableLoader.cs` 293行目を `tcs` パターンに置換 |
| `error CS0200 AssetBundleProviderType cannot be assigned` | `AddressablesUtility.cs` 70〜72行目をコメントアウト |
| `Failed to find entry-points (Burst)` | 無害な警告のため無視 |

---

## STEP 6〜7: PLATEAU 都市データのダウンロード・インポート

1. [G空間情報センター](https://www.geospatial.jp/ckan/dataset) で「名古屋」を検索し CityGML (V4) をダウンロード・展開
2. PLATEAU → PLATEAU SDKを開く → インポートタブ → 基本座標系: `7`（第7系 / 名古屋市）→ シーンに配置 → マップ範囲を選択 → インポート（数分かかる）

---

## STEP 8: スクリプトの配置

| 配置先 | 内容 |
|---|---|
| `Assets/Scripts/` | DroneController.cs / CameraFollow.cs / PropellerEffect.cs / AsyncOperationExtensions.cs / MinimapCamera.cs / MinimapMarker.cs / MinimapToggle.cs / DirectionLabels.cs / HelpUI.cs / TitleReturnUI.cs / PropellerAudio.cs / DroneHUD.cs / BatterySystem.cs |
| `Assets/Scripts/Weather/` | WeatherLayer.cs / SteadyWindLayer.cs / GustLayer.cs / ThermalLayer.cs / DowndraftLayer.cs / RainLayer.cs / FogLayer.cs / WeatherPreset.cs / WeatherSystem.cs / WeatherLogger.cs / WeatherUI.cs / RainFollowDrone.cs / BuildingWeatherEffect.cs |
| `Assets/Scripts/Network/` | DroneSpec.cs / DroneSpecLoader.cs / GameSettings.cs / TCPServer.cs / DroneSpawner.cs / DroneNetworkController.cs / DroneStateBroadcaster.cs / SensorModel.cs / PathPlanner.cs / AutopilotController.cs |
| `Assets/Scripts/Title/` | TitleSceneManager.cs |
| `Assets/Editor/` | DroneModelCreator.cs / MinimapSetup.cs / HelpSetup.cs / HUDSetup.cs / WeatherSetup.cs / BatterySetup.cs / TitleSetup.cs / TCPSetup.cs |
| `Assets/Shaders/` | MinimapCircle.shader |
| `Assets/Resources/DroneSpecs/` | Drone_TypeA.json / Drone_TypeB.json / Drone_TypeC.json / Drone_TypeD.json |

---

## STEP 9: キーボードモードシーンのセットアップ（NagoyaCity）

> シーン名は `NagoyaCity`（Project ウィンドウで `SampleScene` を右クリック → Rename）
> セットアップ完了後に File → Save As で `Assets/Scenes/NagoyaCity.unity` として保存すること

1. Hierarchy → Create Empty → 名前「Drone」→ Rigidbody・DroneController を追加
2. Droneの子に Cylinder を4つ作成（PropFL/FR/BL/BR）  
   Position: FL(-0.5,0.1,0.5) / FR(0.5,0.1,0.5) / BL(-0.5,0.1,-0.5) / BR(0.5,0.1,-0.5)  
   各プロペラに PropellerEffect を追加・Drone を設定。FR/BL は Clockwise のチェックを外す
3. Main Camera に CameraFollow を追加・Target に「Drone」を設定
4. Drone の Position Y を 1 に設定
5. Drone を選択 → **Drone → Create Drone Model**
6. **Drone → Setup Minimap** を実行
7. Create Empty → 名前「UIManager」→ MinimapToggle を追加・MinimapCanvas / MinimapCamera をドラッグ設定
8. **Drone → Setup Help UI** を実行
9. **Drone → Setup HUD** を実行
10. Drone に PropellerAudio コンポーネントを追加（AudioSource が自動追加される）
11. **Drone → Setup Battery** を実行
12. **Drone → Setup Weather System** を実行

---

## STEP 10: TCPモードシーンのセットアップ（NagoyaCityTCP）

> **必須設定**: Edit → Project Settings → Player → Resolution and Presentation → **Run In Background** にチェック（これがないとアプリにフォーカスを移したときUnityが停止する）

**［事前］Drone プレハブを作成する（NagoyaCityシーンで実施）**

1. `NagoyaCity` シーンを開く
2. Hierarchy の「Drone」を選択 → **Drone → Create Drone Model** を実行（マテリアルが `Assets/Materials/Drone/` にアセット保存される）
3. Hierarchy の「Drone」を Project ウィンドウの `Assets/` へドラッグ → `Assets/Drone.prefab` が作成される

**［本手順］NagoyaCityTCPシーンのセットアップ**

4. `NagoyaCity` シーンを複製して `NagoyaCityTCP` として `Assets/Scenes/` に保存
5. `NagoyaCityTCP` シーンを開いた状態で **Drone → Add TCP Manager** を実行（TCPManager が自動追加される）
6. **Hierarchy の「Drone」を削除する**（残しておくとキーボード操作が混在し、カメラが元 Drone を追従し続ける。削除後 CameraFollow の Target が None になるが、自動検索機能があるため問題ない）
7. **Drone → Setup HUD** を実行（TCPシーン用に再生成：Hボタンなし・DroneIndexText あり）
8. **Drone → Setup Help UI** を実行（TCPシーン用テキストとパネルサイズで再生成）
9. **Drone → Setup Minimap** を実行（TCPシーン検出で Drone.prefab を対象に設定。target は実行時に CameraFollow.target を自動参照）
10. **Drone → Setup Battery** を実行（TCPシーン検出で Drone.prefab の capacity を設定。初期 isEnabled は実行時にシーン名で自動設定）
11. `TCPManager` の `DroneSpawner` コンポーネントの各フィールドを設定:
   - `Drone Prefab` → `Assets/Drone`
   - `Spawn Root` → ドローンを出発させたい地点の空 GameObject（任意。未設定時は原点）
   - `Grid Spacing X / Z` → デフォルト 3m（2×4グリッドの列・行間隔）
   - `Base Cruise Alt` → デフォルト 30m（機体0の巡航高度）
   - `Cruise Alt Step` → デフォルト 2m（機体ごとの巡航高度オフセット）
12. Cmd+S でシーンを保存

---

## STEP 11: タイトルシーンのセットアップ

> STEP 9・10 が完了し `Assets/Scenes/` に NagoyaCity.unity・NagoyaCityTCP.unity が存在する状態で実行すること

1. **Drone → Setup Title Scene** を実行（`Assets/Scenes/TitleScene.unity` が自動生成・Build Settings に3シーンが自動登録される）
2. File → Build Settings でシーン順を確認（TitleScene を 0 番目に）

---

## STEP 12: 飛行ログ可視化ツールのセットアップ

```bash
cd LogVisualizer
pip3 install pandas matplotlib pillow pyinstaller pyobjc-framework-Cocoa
bash build_app.sh
```

ビルド後、`LogVisualizer/dist/DroneLogVisualizer.app` をダブルクリックで起動。
Settings → Import Folder に `DroneSimulator/WeatherLogs/`、Export Folder に `LogVisualizer/exports/` を指定。

---

## STEP 13: TCP クライアントアプリのセットアップ

```bash
cd TCPClient
pip3 install pyinstaller
bash build_app.sh
```

ビルド後、`Drone/DroneTCPClient.app` をダブルクリックで起動。

**操作手順（マルチドローン）**:
1. IP・Port を入力
2. 「＋ドローン追加」で機体行を追加（最大8機）
3. 各行で機体（TypeA/B/C/D）と Goal 座標を設定
4. 「全機接続」→ 全行が接続されステータスが「接続済み」になる
5. 「Autopilot 一斉開始」→ 全機が同時に経路計算・飛行を開始

---

## 機体スペックの追加

`Assets/Resources/DroneSpecs/` に JSON を置くだけで自動認識（コード変更不要）。
フォーマットは `Drone_TypeA.json` を参照。

**速度設計の注意点**: `maxVerticalSpeed` は `maxHorizontalSpeed` より小さい値を推奨（デフォルト比: 6/19）。垂直加速力 `verticalSpeed` も同様（デフォルト比: 5/15）。高度差の大きな自律飛行ルートでは `waypointTimeout`（15秒）に達しやすいため、goal.y は極端な値を避けるか `maxVerticalSpeed` を引き上げること。

**天候パラメータの注意点**: `windResistanceFactor` は風力への感度係数（小さいほど風に強い）。`maxOperatingWindSpeedMs` を超える風速では autopilot の水平入力が自動的に絞られる。TypeB・TypeD は質量が軽く係数も大きいため、Storm プリセット下では到達に時間がかかることがある。TypeD は `maxOperatingWindSpeedMs: 8.0` のため強風環境には不向き。TypeC は `windResistanceFactor: 0.5 / maxOperatingWindSpeedMs: 20.0` で最も風に強い。
