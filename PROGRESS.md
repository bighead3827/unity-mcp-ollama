# Unity-MCP-Ollama 実装進捗

## プロジェクト概要
このプロジェクトは、[justinpbarnett/unity-mcp](https://github.com/justinpbarnett/unity-mcp)を拡張し、Claude APIの代わりにOllamaを使用してローカルLLMとUnityを連携させるものです。

## 現在の状態
- リポジトリ作成済み
- 基本ドキュメント作成済み
- Python側の実装完了
  - Ollamaとの連携コード実装済み
  - MCP対応レスポンスフォーマットの実装済み
  - 設定ファイルの修正済み
  - カスタムTCPサーバー実装完了
- Unity側の実装完了
  - Ollama設定UI追加済み
  - 接続処理の修正済み
  - チャットインターフェース追加済み
  - エラー耐性の強化とフォールバックメカニズム追加済み
  - TCPサーバーへの接続機能実装済み
  - Unityコマンド実行機能実装済み
  - オブジェクト検索機能（find_objects_by_name）実装済み
  - トランスフォーム操作機能（transform）実装済み
- インストールとセットアップ手順の詳細化完了
- バグ修正済み
  - デフォルトモデルをgemma3:12bに変更
  - Process_user_requestコマンド処理の実装
  - Pythonサーバーへのリクエスト転送実装
  - 接続状態のトラッキング改善
  - ログ出力の最適化
  - カスタムTCPサーバー実装による連携問題解決
  - find_objects_by_nameとtransformコマンドの実装

## 実装すべき機能
1. ✅ リポジトリ作成
2. ✅ 基本ドキュメント作成
3. ✅ Pythonパッケージの修正
   - ✅ Ollamaとの連携コード実装 (`ollama_connection.py`)
   - ✅ MCP対応レスポンスフォーマットの実装 (`server.py`内のプロセス関数)
   - ✅ 設定ファイルの修正 (`config.py`のOllama設定)
   - ❌ FastMCPでのTCPサーバーモードの実装 → MCPライブラリでサポートされていない
   - ✅ カスタムTCPサーバーの実装 (`tcp_server.py`)
4. ✅ Unityパッケージの修正
   - ✅ Ollama設定UI追加
   - ✅ 接続処理の修正
   - ✅ チャットインターフェース追加
   - ✅ エラー処理の強化
   - ✅ カスタムTCPサーバーとの連携
   - ✅ Unityコマンド実行機能の実装
   - ✅ オブジェクト検索機能（find_objects_by_name）の実装
   - ✅ トランスフォーム操作機能（transform）の実装
5. ✅ インストールとセットアップ手順の詳細化
6. ✅ テスト実施とバグ修正
   - ✅ チャットエラー「Error: Command process_user_request was received」の修正
   - ✅ Ollamaモデル設定の修正
   - ✅ チャットリクエストがエコーバックされる問題の修正
   - ✅ Pythonサーバー接続エラーの処理
   - ✅ 接続ステータスログの最適化
   - ✅ TCP接続問題の解決のためのカスタムサーバー実装
   - ✅ Unity側のエラー処理強化と接続安定性向上
   - ✅ find_objects_by_nameとtransformコマンドの実装

## 優先度の高いタスク
1. ✅ Python側のOllama連携コードの実装
2. ✅ MCP対応レスポンスフォーマットの実装
3. ✅ Unityパッケージの設定画面修正
4. ✅ gemma3:12bとdeepseek-r:14bモデルとの連携検討
5. ✅ Unity側のコマンド処理の実装
6. ✅ カスタムTCPサーバーの実装
7. ✅ ユーザーエクスペリエンスの改善（ログ最適化など）
8. ✅ エラー処理の強化とフォールバックメカニズムの追加
9. ✅ Unityコマンド（オブジェクト作成など）の実装
10. ✅ オブジェクト検索とトランスフォーム操作の実装

## 解決した制約と課題
元々のMCPライブラリではTCPモードがサポートされていないという制約がありましたが、この問題を解決するために以下の対策を実施しました：

1. Python側の実装:
   - `tcp_server.py`: 独自のTCPサーバーを実装
   - Ollamaとの連携機能を維持しつつ、TCP経由での通信をサポート
   - リクエスト処理とレスポンス生成の完全実装

2. Unity側の実装:
   - TCPサーバーへの接続機能を実装
   - 接続エラー時のフォールバックメカニズムを実装
   - Unityコマンド（オブジェクト作成、変形など）の実行機能を追加
   - find_objects_by_nameとtransformコマンドの実装を追加

## 今後の課題
- さらに多くのUnityコマンドの実装（マテリアル、スクリプト、アセット関連など）
- より高度なエラーハンドリングの実装
- Unity側でのUIの改善
- 自然言語による複雑な指示の解釈精度向上
- その他のLLMモデルとの互換性テスト
- パフォーマンス最適化

## 実装メモ
### 2025-03-19
- リポジトリ作成
- 進捗管理ファイル作成
- 実装計画立案
- Python側の実装完了
  - `ollama_connection.py`: Ollamaとの連携を処理するモジュール作成
  - `config.py`: Ollama固有の設定を追加
  - `server.py`: Ollamaを使用した処理とMCP関数の実装
  - `pyproject.toml`: 依存関係の更新
  - `Python/README.md`: Python側の使用方法ドキュメント追加
- Unity側の実装完了
  - `MCPEditorWindow.cs`: Ollama設定UI、チャットインターフェース追加
  - `UnityMCPBridge.cs`: ソケット通信の基盤コード実装
  - `package.json`: パッケージ情報の更新
- ドキュメントの更新
  - 詳細なセットアップと使用手順を`README.md`に追加
- バグ修正
  - Ollama連携エラー「Model llama3 not found in Ollama」を解決
  - `local_config.json`を追加して使用モデルを`gemma3:12b`に変更
  - チャットエラー「Error: Command process_user_request was received」を解決
  - `UnityMCPBridge.cs`のコマンド処理を修正してプロセスユーザーリクエスト処理を実装
  - チャットリクエストがエコーバックされる問題を解決するために、シミュレーション応答機能を実装
  - 接続エラー処理を改善し、シミュレートされた応答機能を実装
  - 接続状態のログ出力を最適化し、状態変化時のみ出力するように変更
  - `ObjectCommands.cs`を追加し、find_objects_by_nameとtransformコマンドを実装
  - ProcessExtractedCommandsメソッドを更新して新しいコマンドを追加

### 2025-03-20
- TCP接続の問題分析と検証
  - Python側のサーバーがTCPモードをサポートしていないことを確認
  - `server.py`を修正してstdioモードのみに変更
  - 代替としてシミュレーションモードを実装

- 独自のTCPサーバー実装
  - `tcp_server.py`: asyncioを使用した完全な非同期TCPサーバーを実装
  - MCPプロトコルに対応するコマンドハンドラーを実装
  - Ollamaとの連携機能を維持
  - エラーハンドリングとログ機能の強化

- Unity側の機能強化
  - TCPサーバーへの接続処理を実装
  - Unityコマンド（create_object, set_object_transform, delete_objectなど）の実行機能を追加
  - LLM応答からコマンドを抽出して実行する機能を追加
  - エラー処理とフォールバックメカニズムの強化
  - find_objects_by_nameとtransformコマンドの実装を追加
  - オブジェクト操作関連の機能を専用の`ObjectCommands.cs`クラスに分離

## 実装詳細
### カスタムTCPサーバーの実装
- `tcp_server.py`: 新しく作成した独自のTCPサーバー実装
  - asyncioを使用した非同期サーバー
  - クライアント接続の管理
  - JSONメッセージの処理
  - Ollamaとの連携を維持
  - 以下のコマンドをサポート:
    - `process_user_request`: ユーザーのプロンプトを処理しOllamaに送信
    - `get_ollama_status`: Ollamaの状態を確認
    - `configure_ollama`: Ollamaの設定を変更

### Ollamaとの連携
- `ollama_connection.py`モジュールを作成し、Ollama APIとの通信を担当
- 主な機能:
  - APIとの接続・応答取得
  - ローカルLLMからのテキスト応答をMCPコマンドに変換

### 設定機能の拡張
- `config.py`ファイルにOllama固有の設定を追加:
  - ホスト/ポート設定
  - 使用モデル設定（`deepseek-r:14b`と`gemma3:12b`に対応）
  - システムプロンプト設定
  - 温度パラメータなど
- `local_config.json`を追加し、デフォルトモデルを`gemma3:12b`に変更

### サーバー機能の拡張
- 従来の`server.py`: stdioモードでMCP機能を提供
- 新しい`tcp_server.py`: TCPモードでOllama連携機能を提供
  - ポート6500でリッスン
  - JSONフォーマットのリクエスト/レスポンス処理
  - LLM応答からのコマンド抽出機能

### Unity側の改修
- `MCPEditorWindow.cs`:
  - Ollamaの設定UI
  - モデル選択インターフェース（`deepseek-r:14b`と`gemma3:12b`）
  - チャットインターフェース
  - 状態表示の改善
  - 接続状態のトラッキング機能
  - 非同期メッセージ応答の処理と表示機能
- `UnityMCPBridge.cs`:
  - ソケット通信コードの改善
  - コマンド処理の実装（`process_user_request`等）
  - エラーハンドリングの強化
  - TCPサーバーへの接続機能
  - Unityコマンド（create_object, set_object_transform, delete_objectなど）の実装
  - find_objects_by_nameとtransformコマンドの追加
  - エラー時のフォールバックメカニズム
  - メッセージIDに基づく応答の保存と取得機能
- `ObjectCommands.cs`:
  - オブジェクト検索機能（FindObjectsByName）の実装
  - トランスフォーム操作機能（SetTransform）の実装
  - Vector3変換ヘルパーメソッド

### Unityコマンドの実装
- 基本的なオブジェクト操作:
  - `CreateObject`: プリミティブの作成（Cube, Sphere, Cylinder, Planeなど）
  - `SetObjectTransform`: 位置、回転、スケールの設定
  - `DeleteObject`: オブジェクトの削除
  - `EditorAction`: エディタコマンド（PLAY, PAUSE, STOP, SAVEなど）
  - `FindObjectsByName`: 名前によるオブジェクト検索
  - `TransformObject`: オブジェクトのトランスフォーム操作

### バグ修正詳細
1. **モデル設定の修正**
   - 問題: デフォルトで`llama3`モデルを使用しようとしていたが、実際にはそのモデルがOllamaにインストールされていなかった
   - 解決策: `local_config.json`ファイルを作成して`gemma3:12b`モデルに設定を変更

2. **チャットコマンド処理の修正**
   - 問題: Unity側の`UnityMCPBridge.cs`が`process_user_request`コマンドを適切に処理していなかった
   - 解決策: `ExecuteCommand`メソッドを拡張して特定のコマンドタイプに対する処理を実装

3. **チャットエコーバックの修正**
   - 問題: チャットメッセージが単にエコーバックされるだけで、実際にOllamaモデルを活用していなかった
   - 解決策: 
     - カスタムTCPサーバーを実装してOllamaと連携
     - 非同期処理を活用して、Unityのメインスレッドをブロックしないように実装
     - デバッグログの強化で問題追跡を容易に

4. **接続エラー処理の改善**
   - 問題: MCPサーバーへの接続試行でエラーが発生していた
   - 解決策:
     - カスタムTCPサーバーを実装
     - 接続エラー時のフォールバックメカニズムを強化
     - 適切なエラーメッセージを表示

5. **連続的なログ出力の修正**
   - 問題: 接続状態に変化がなくても、同じログが繰り返し出力されていた
   - 解決策:
     - 接続状態をトラッキングし、状態変化時のみログを出力するように変更
     - ユーザーエクスペリエンスの改善

6. **TCPサーバー接続問題の解決**
   - 問題: MCPライブラリがTCPをサポートしていないため、Unity-Python間の通信ができなかった
   - 解決策:
     - 独自のTCPサーバー（`tcp_server.py`）を実装
     - Unity側のTCP接続処理を対応する形に修正
     - エラー処理とフォールバックメカニズムの追加

7. **Unityコマンド実行機能の追加**
   - 問題: LLM応答からUnityコマンドを抽出して実行する機能がなかった
   - 解決策:
     - `ProcessExtractedCommands`メソッドを実装
     - 基本的なUnityコマンド（CreateObject, SetObjectTransform, DeleteObjectなど）を実装
     - JSON応答からコマンドを抽出する機能を追加

8. **find_objects_by_nameとtransformコマンドの実装**
   - 問題: これらのコマンドが実装されておらず、エラーが発生していた
   - 解決策:
     - 新しい`ObjectCommands.cs`クラスを作成
     - FindObjectsByNameメソッドとSetTransformメソッドを実装
     - UnityMCPBridge.csのProcessExtractedCommandsメソッドを更新

## 実装方法（アセットとして使用）

パッケージマネージャーでの互換性問題を回避するために、アセットとして直接プロジェクトに組み込む方法を採用しました。

### 実装手順

1. **リポジトリをクローン**:
   ```
   git clone https://github.com/ZundamonnoVRChatkaisetu/unity-mcp-ollama.git
   ```

2. **Editorファイルをコピー**:
   リポジトリの`Editor`フォルダを`Assets/UnityMCPOllama/Editor`にコピーします。
   ```
   # Unity プロジェクト内にフォルダを作成
   mkdir -p Assets/UnityMCPOllama
   
   # Editorフォルダをコピー
   cp -r unity-mcp-ollama/Editor Assets/UnityMCPOllama/
   ```

3. **Python環境のセットアップ**:
   プロジェクト外の安全な場所にPython環境を構築します。
   ```
   mkdir -p PythonMCP
   cd PythonMCP
   
   # リポジトリのPythonフォルダをコピー
   cp -r ../unity-mcp-ollama/Python .
   
   # 仮想環境を作成
   python -m venv venv
   
   # 仮想環境を有効化
   # Windowsの場合:
   venv\\Scripts\\activate
   # macOS/Linuxの場合:
   source venv/bin/activate
   
   # 依存関係をインストール
   cd Python
   pip install -e .
   ```

4. **カスタムTCPサーバーの起動**:
   Python環境を有効化した状態で以下のコマンドを実行します。
   ```
   # 仮想環境内で
   python tcp_server.py
   ```
   "TCP Server running on localhost:6500" というメッセージが表示されることを確認します。

### 使用方法

1. **カスタムTCPサーバーの起動**:
   - 仮想環境を有効化し、`python tcp_server.py`を実行
   - サーバーがポート6500で起動することを確認

2. **Unity側の起動と確認**:
   - Unityエディタで「Window > Unity MCP」メニューを開く
   - チャットインターフェースで指示を入力（例：「赤い立方体を作成してください」）
   - LLMからの応答と、対応するUnityコマンドが実行されることを確認

3. **サポートされるUnityコマンド**:
   - オブジェクト作成: cube, sphere, cylinder, plane, emptyなど
   - トランスフォーム設定: 位置、回転、スケールの変更
   - オブジェクト削除
   - エディタ操作: play, pause, stop, saveなど
   - オブジェクト検索: 名前に基づいて検索
   - トランスフォーム操作: 位置・回転・スケールの一括設定

### 確認事項

- カスタムTCPサーバーが起動していることを確認（ポート6500）
- Ollamaが正しくインストールされ、対応モデルがダウンロードされていることを確認
- Unity側でコマンドが正しく実行されることを確認
- エラー発生時に適切なエラーメッセージが表示されることを確認

## 参考リソース
- [元リポジトリ](https://github.com/justinpbarnett/unity-mcp)
- [Ollama API ドキュメント](https://github.com/ollama/ollama/blob/main/docs/api.md)
- [Model Context Protocol (MCP) 仕様](https://github.com/anthropics/anthropic-cookbook/tree/main/model_context_protocol)