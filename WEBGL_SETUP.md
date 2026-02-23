# WebGL ビルド & GitHub Pages デプロイ セットアップガイド

ThemeParkGame を WebGL としてビルドし、GitHub Pages で公開するための手順書です。

## 公開URL

https://rio2ryo.github.io/ThemeParkGame/

## 概要

GitHub Actions で [GameCI](https://game.ci/) を使用し、Unity プロジェクトを自動的に WebGL ビルドします。
ビルド成功後、[JamesIves/github-pages-deploy-action](https://github.com/JamesIves/github-pages-deploy-action) により `gh-pages` ブランチに自動デプロイされます。

### ワークフロー構成

```
push (main/master)
  → チェックアウト
  → Library キャッシュ復元
  → Unity ライセンスアクティベーション
  → WebGL ビルド (Unity 2021.3.35f1)
  → ビルド成果物アップロード (Artifacts)
  → GitHub Pages デプロイ (gh-pages ブランチ)
```

ワークフロー定義: `.github/workflows/build-webgl.yml`

---

## 1. 前提条件

- GitHub リポジトリの Settings → Pages が有効であること
- Unity アカウント (Personal ライセンスで可)
- リポジトリの Admin 権限 (Secrets 設定に必要)

## 2. GitHub Secrets の設定

リポジトリの **Settings → Secrets and variables → Actions → New repository secret** から以下を追加します。

### 必須シークレット

| シークレット名 | 説明 |
|---|---|
| `UNITY_LICENSE` | Unityライセンスファイル (.ulf) の全内容 |
| `UNITY_EMAIL` | Unity ID に登録したメールアドレス |
| `UNITY_PASSWORD` | Unity ID のパスワード |

### UNITY_LICENSE の取得手順 (初回のみ)

Unity Personal (無料) ライセンスを使用する場合の手順です。

#### ステップ 1: アクティベーションファイルの生成

1. `UNITY_LICENSE` シークレットを**設定せずに**ワークフローを実行します
   - `master` / `main` ブランチに push するか、Actions タブから手動実行 (Run workflow)
2. ワークフローは「UNITY_LICENSE シークレットが設定されていません」のエラーで失敗します
3. 失敗したワークフローの **Artifacts** セクションから `Unity_Activation_File` をダウンロードします
4. ダウンロードした ZIP を解凍すると `.alf` ファイルが含まれています

#### ステップ 2: ライセンスファイルの取得

1. ブラウザで https://license.unity3d.com/manual にアクセス
2. Unity アカウントでログイン
3. `.alf` ファイルをアップロード
4. ライセンスタイプで **「Unity Personal」** を選択
5. 利用条件を確認して **「Download license file」** をクリック
6. `.ulf` ファイルがダウンロードされます

#### ステップ 3: シークレットへの登録

1. ダウンロードした `.ulf` ファイルをテキストエディタで開く
2. **ファイルの内容をすべてコピー** (XML形式のテキスト)
3. GitHub リポジトリの **Settings → Secrets → Actions → New repository secret**
4. Name: `UNITY_LICENSE`、Value: コピーしたテキストを貼り付け
5. **Add secret** をクリック

#### ステップ 4: ワークフロー再実行

1. Actions タブから失敗したワークフローを開く
2. **Re-run all jobs** をクリック
3. ビルドが成功し、GitHub Pages にデプロイされます

---

## 3. GitHub Pages の有効化

1. リポジトリの **Settings → Pages** を開く
2. **Source** で **「Deploy from a branch」** を選択
3. **Branch** で **「gh-pages」** / **「/ (root)」** を選択
4. **Save** をクリック

初回のワークフロー成功後に `gh-pages` ブランチが自動作成されます。

---

## 4. ビルドのトリガー

### 自動トリガー

`main` または `master` ブランチへの push で自動的にビルドが開始されます。

### 手動トリガー

1. リポジトリの **Actions** タブを開く
2. 左メニューから **「Build WebGL and Deploy to GitHub Pages」** を選択
3. **「Run workflow」** ボタンをクリック
4. ブランチを選択して **「Run workflow」** を実行

---

## 5. トラブルシューティング

### ビルドが失敗する場合

| エラー | 原因 | 対処法 |
|---|---|---|
| `UNITY_LICENSE is not set` | ライセンスシークレット未設定 | 上記「UNITY_LICENSE の取得手順」に従う |
| `License has expired` | ライセンスの有効期限切れ | 新しいライセンスを取得して UNITY_LICENSE を更新 |
| `Build failed with errors` | Unity コンパイルエラー | ローカルで WebGL ビルドを試してエラーを確認 |
| `Out of memory` | メモリ不足 | ビルド設定でストリッピングレベルを調整 |

### GitHub Pages にアクセスできない場合

1. **Settings → Pages** で GitHub Pages が有効か確認
2. `gh-pages` ブランチが存在するか確認
3. DNS 伝播に数分かかる場合があるので待つ
4. ブラウザのキャッシュをクリアしてリロード

### WebGL ビルドが真っ白になる場合

- ブラウザの開発者ツール (F12) → Console でエラーを確認
- `SceneBootstrapper` がシーン内に配置されているか確認
- WebGL の圧縮設定 (Brotli/Gzip) とサーバー設定の不一致がないか確認

### ローカルでの WebGL テスト

WebGL ビルドはファイルシステムから直接開けません。ローカルサーバーが必要です:

```bash
# Python 3 を使用
cd build/WebGL/ThemeParkGame
python3 -m http.server 8080

# ブラウザで http://localhost:8080 にアクセス
```

---

## 6. ワークフローのカスタマイズ

### Unity バージョンの変更

`.github/workflows/build-webgl.yml` の `unityVersion` を変更:

```yaml
with:
  unityVersion: 2021.3.35f1  # ← ここを変更
```

### ビルドキャッシュ

`Library` フォルダをキャッシュすることで、2回目以降のビルドが高速化されます。
キャッシュキーは `Assets/`, `Packages/manifest.json`, `ProjectSettings/` のハッシュに基づいています。

### 成果物の保持期間

ビルド成果物 (Artifacts) はデフォルトで 14 日間保持されます。
`.github/workflows/build-webgl.yml` の `retention-days` で変更可能です。

---

## 7. 使用ツール・アクション

| ツール | バージョン | 用途 |
|---|---|---|
| [game-ci/unity-builder](https://github.com/game-ci/unity-builder) | v4 | Unity WebGL ビルド |
| [game-ci/unity-request-activation-file](https://github.com/game-ci/unity-request-activation-file) | v2 | ライセンスアクティベーションファイル生成 |
| [JamesIves/github-pages-deploy-action](https://github.com/JamesIves/github-pages-deploy-action) | v4 | GitHub Pages デプロイ |
| [actions/cache](https://github.com/actions/cache) | v4 | Library フォルダキャッシュ |
| [actions/upload-artifact](https://github.com/actions/upload-artifact) | v4 | ビルド成果物アップロード |
