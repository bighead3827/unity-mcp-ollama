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
  - TCPサーバー（ポート6500）としての起動対応完了
- Unity側の実装完了
  - Ollama設定UI追加済み
  - 接続処理の修正済み
  - チャットインターフェース追加済み
  - エラー耐性の強化とフォールバックメカニズム追加済み
- インストールとセットアップ手順の詳細化完了
- バグ修正済み
  - デフォルトモデルをgemma3:12bに変更
  - Process_user_requestコマンド処理の実装
  - Pythonサーバーへのリクエスト転送実装
  - 接続状態のトラッキング改善
  - ログ出力の最適化
  - シミュレートされた応答からOllama実装への切り替え完了
  - TCPサーバー接続問題の修正（stdio → tcp）
  - Unity側のエラー処理強化と接続安定性向上

## 実装すべき機能
1. ✅ リポジトリ作成
2. ✅ 基本ドキュメント作成
3. ✅ Pythonパッケージの修正
   - ✅ Ollamaとの連携コード実装 (`ollama_connection.py`)
   - ✅ MCP対応レスポンスフォーマットの実装 (`server.py`内のプロセス関数)
   - ✅ 設定ファイルの修正 (`config.py`のOllama設定)
   - ✅ TCPサーバーモードの実装
4. ✅ Unityパッケージの修正
   - ✅ Ollama設定UI追加
   - ✅ 接続処理の修正
   - ✅ チャットインターフェース追加
   - ✅ エラー処理の強化
5. ✅ インストールとセットアップ手順の詳細化
6. ✅ テスト実施とバグ修正
   - ✅ チャットエラー「Error: Command process_user_request was received」の修正
   - ✅ Ollamaモデル設定の修正
   - ✅ チャットリクエストがエコーバックされる問題の修正
   - ✅ Pythonサーバー接続エラーの処理
   - ✅ 接続ステータスログの最適化
   - ✅ シミュレートされた応答からOllama実装への切り替え完了
   - ✅ サーバー接続エラー「The operation is not allowed on non-connected sockets」の修正
   - ✅ Unity側のエラー処理強化と接続安定性向上

## 優先度の高いタスク
1. ✅ Python側のOllama連携コードの実装
2. ✅ MCP対応レスポンスフォーマットの実装
3. ✅ Unityパッケージの設定画面修正
4. ✅ gemma3:12bとdeepseek-r:14bモデルとの連携テスト
5. ✅ Unity側のコマンド処理の実装
6. ✅ Python MCPサーバーへのリクエスト転送実装
7. ✅ ユーザーエクスペリエンスの改善（ログ最適化など）
8. ✅ Python MCPサーバーのポート6500へのリクエスト転送機能の完成
9. ✅ エラー処理の強化とフォールバックメカニズムの追加

## 今後の課題
- その他のLLMモデルとの互換性テスト
- パフォーマンス最適化
- より高度なエラーハンドリングの実装
- Unity側でのツール機能の拡張

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
  - チャットリクエストがエコーバックされる問題を解決するために、MCP Python サーバーへの転送を実装
  - 接続エラー処理を改善し、シミュレートされた応答機能を実装
  - 接続状態のログ出力を最適化し、状態変化時のみ出力するように変更
  - シミュレートされた応答機能からOllama実装への切り替え実施

### 2025-03-20
- シミュレーションモードから実際のOllama実装へ切り替え
  - `UnityMCPBridge.cs`のシミュレーションコードをコメントアウト
  - 実際のMCPサーバー接続（ポート6500）へのコードを有効化
  - `get_ollama_status`と`configure_ollama`コマンドをPythonサーバーに直接転送するよう修正
  - メッセージ応答の処理方法を改善（応答をsimulatedResponsesディクショナリに保存）
- サーバー接続エラー「The operation is not allowed on non-connected sockets」の修正
  - Python側の`server.py`を修正してTCPモードで起動するように変更
  - `host`パラメータの削除（FastMCP.run()はこのパラメータをサポートしていなかった）
  - エラー発生時のフォールバックとしてstdioモードを追加
- Unity側のエラー処理強化と接続安定性向上
  - `UnityMCPBridge.cs`の接続処理を改善
  - 接続タイムアウト処理の追加と待機時間の調整
  - MCP接続が失敗した場合のフォールバックとしてシミュレーション応答機能を追加
  - `get_ollama_status`と`configure_ollama`コマンドにもタイムアウト処理を追加
  - エラーメッセージの改善と詳細化
  - 停止処理時の例外処理を追加

## 実装詳細
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
- `server.py`を更新して以下の機能を追加:
  - Ollamaとの接続ライフサイクル管理
  - ユーザー自然言語リクエストの処理と実行機能
  - LLM応答からのコマンド抽出と実行
  - Ollama設定の動的更新
  - TCPサーバーモードでのポート6500での起動対応
  - エラー時のフォールバックとしてstdioモードを追加

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
  - Python MCPサーバーへのリクエスト転送機能の実装
  - シミュレートされた応答機能を実際のOllama実装に切り替え
  - 接続状態トラッキングの実装
  - メッセージIDに基づく応答の保存と取得機能
  - 接続タイムアウト処理と待機時間の調整
  - 接続失敗時のフォールバックメカニズムの強化
  - 適切なエラーメッセージの提供

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
     - `ForwardToMCPServer`メソッドを実装してPython側のMCPサーバーに要求を転送
     - 非同期処理を活用して、Unityのメインスレッドをブロックしないように実装
     - デバッグログの強化で問題追跡を容易に

4. **接続エラー処理の改善**
   - 問題: ポート6500に接続できないエラーが発生していた
   - 解決策:
     - シミュレーション応答からOllama実装へ切り替え
     - 実際のMCPサーバー接続（ポート6500）を有効化
     - `get_ollama_status`と`configure_ollama`コマンドをPythonサーバーに直接転送

5. **連続的なログ出力の修正**
   - 問題: 接続状態に変化がなくても、同じログが繰り返し出力されていた
   - 解決策:
     - 接続状態をトラッキングし、状態変化時のみログを出力するように変更
     - ユーザーエクスペリエンスの改善

6. **チャット応答表示の問題修正**
   - 問題: シミュレートされた応答がチャットUIに表示されなかった
   - 解決策:
     - メッセージIDを使用して応答を追跡する仕組みを追加
     - `UnityMCPBridge`クラスに応答保存・取得メソッドを追加
     - `MCPEditorWindow`クラスに定期的に応答をチェックするロジックを追加
     - 非同期応答をチャット履歴に反映する仕組みを実装

7. **サーバー接続エラーの修正**
   - 問題: 「The operation is not allowed on non-connected sockets」エラーが発生していた
   - 解決策:
     - Python側の`server.py`を修正してTCPモード（ポート6500）で起動するように変更
     - `host`パラメータを削除（FastMCP.run()がこのパラメータに対応していなかった）
     - TCP起動時のエラーハンドリングを追加し、stdio へのフォールバックを実装

8. **Unity側のエラー処理強化**
   - 問題: Unity側でMCPサーバーへの接続が不安定で、エラーが発生すると回復できなかった
   - 解決策:
     - 接続処理を改善し、タイムアウト処理を追加
     - 接続失敗時のフォールバックメカニズムを強化
     - MCP接続が失敗した場合でも適切なシミュレーション応答を返すように修正
     - エラーメッセージの改善でユーザーが問題を特定しやすく

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

4. **サーバーの起動**:
   Python環境を有効化した状態で以下のコマンドを実行します。
   ```
   # 仮想環境内で
   python server.py
   ```
   "Starting MCP TCP server on port 6500" というメッセージが表示されることを確認します。

### 接続エラーが発生する場合

サーバー接続に問題が発生する場合は、以下を確認してください:

1. **Unityエディタと現在の状態をリセットする**:
   - Unityエディタを一度完全に終了し、再起動してみてください
   - 再起動前にPythonサーバーも一度停止し、再度起動してください

2. **ポートの確認**:
   - ポート6500が他のアプリケーションで使用されていないことを確認してください
   - Windowsの場合: `netstat -ano | findstr 6500` を実行して確認

3. **Ollamaの状態確認**:
   - Ollamaが正常に動作していることを確認
   - `ollama list` コマンドで必要なモデル（gemma3:12b）が利用可能かを確認

4. **ログの確認**:
   - Pythonサーバー側のログでエラーメッセージを確認
   - Unityコンソールのログを確認して具体的なエラーを特定

5. **フォールバックモードの使用**:
   - 接続に問題がある場合でも、Unityは自動的にシミュレーションモードにフォールバックします
   - エラーの詳細はチャット応答に表示されます

### 確認事項

- Unity内で「Window > Unity MCP」メニューが表示されることを確認
- Ollamaが正しくインストールされ、対応モデルがダウンロードされていることを確認
- Python環境が正しく設定されていることを確認
- サーバー起動時に、正しいモデル（`gemma3:12b`または`deepseek-r:14b`）が認識されていることを確認
- Unity側のブリッジが正常に起動し、接続状態が「Running」と表示されることを確認
- チャットインターフェースでメッセージを送信すると、Ollamaからの応答が表示されることを確認
- Python側のサーバーログにリクエストと応答が表示されることを確認

## 参考リソース
- [元リポジトリ](https://github.com/justinpbarnett/unity-mcp)
- [Ollama API ドキュメント](https://github.com/ollama/ollama/blob/main/docs/api.md)
- [Model Context Protocol (MCP) 仕様](https://github.com/anthropics/anthropic-cookbook/tree/main/model_context_protocol)