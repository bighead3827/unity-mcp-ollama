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
- Unity側の実装完了
  - Ollama設定UI追加済み
  - 接続処理の修正済み
  - チャットインターフェース追加済み
- インストールとセットアップ手順の詳細化完了

## 実装すべき機能
1. ✅ リポジトリ作成
2. ✅ 基本ドキュメント作成
3. ✅ Pythonパッケージの修正
   - ✅ Ollamaとの連携コード実装 (`ollama_connection.py`)
   - ✅ MCP対応レスポンスフォーマットの実装 (`server.py`内のプロセス関数)
   - ✅ 設定ファイルの修正 (`config.py`のOllama設定)
4. ✅ Unityパッケージの修正
   - ✅ Ollama設定UI追加
   - ✅ 接続処理の修正
   - ✅ チャットインターフェース追加
5. ✅ インストールとセットアップ手順の詳細化
6. ⬜ テスト実施とバグ修正

## 優先度の高いタスク
1. ✅ Python側のOllama連携コードの実装
2. ✅ MCP対応レスポンスフォーマットの実装
3. ✅ Unityパッケージの設定画面修正
4. ⬜ 様々なLLMモデルの互換性テスト
5. ⬜ エラーハンドリングの強化

## 今後の課題
- 様々なLLMモデルとの互換性テスト
- パフォーマンス最適化
- エラーハンドリングの改善
- ユーザーフレンドリーな設定UIの向上

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

## 実装詳細
### Ollamaとの連携
- `ollama_connection.py`モジュールを作成し、Ollama APIとの通信を担当
- 主な機能:
  - APIとの接続・応答取得
  - ローカルLLMからのテキスト応答をMCPコマンドに変換

### 設定機能の拡張
- `config.py`ファイルにOllama固有の設定を追加:
  - ホスト/ポート設定
  - 使用モデル設定（`deepseek-r1:14b`と`gemma3:12b`に対応）
  - システムプロンプト設定
  - 温度パラメータなど

### サーバー機能の拡張
- `server.py`を更新して以下の機能を追加:
  - Ollamaとの接続ライフサイクル管理
  - ユーザー自然言語リクエストの処理と実行機能
  - LLM応答からのコマンド抽出と実行
  - Ollama設定の動的更新

### Unity側の改修
- `MCPEditorWindow.cs`:
  - Ollamaの設定UI
  - モデル選択インターフェース（`deepseek-r1:14b`と`gemma3:12b`）
  - チャットインターフェース
  - 状態表示の改善
- `UnityMCPBridge.cs`:
  - ソケット通信コードの改善
  - コマンド処理の簡素化

## 次のステップ
1. 様々なLLMモデルとのテスト実施
2. エラーハンドリングの強化
3. パフォーマンス最適化

## 参考リソース
- [元リポジトリ](https://github.com/justinpbarnett/unity-mcp)
- [Ollama API ドキュメント](https://github.com/ollama/ollama/blob/main/docs/api.md)
- [Model Context Protocol (MCP) 仕様](https://github.com/anthropics/anthropic-cookbook/tree/main/model_context_protocol)
