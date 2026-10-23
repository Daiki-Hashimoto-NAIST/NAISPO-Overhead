# 正面撮影モードの依存ファイル

正面撮影モードではAzure Kinect Body Tracking SDK 1.1.2を使います。実行用DLLはプロジェクトに同梱し、人体認識モデルは初回に取得します。モデルは約159 MiBあるため、Gitには含めていません。Git LFSの導入も不要です。

## 初回の準備

プロジェクト直下の `Setup-BodyTracking.cmd` を実行してください。既存のモデルがあればサイズとSHA-256を確かめ、そのまま利用します。見つからなければ、MicrosoftがNuGet.orgで公開しているSDK 1.1.2をダウンロードし、モデルだけを取り出します。

ダウンロードは約189 MiBです。Windows PowerShell 5.1で実行でき、追加のパッケージ管理ツールやシステム全体へのSDKインストールは必要ありません。処理が終わったらUnityでWindows版をビルドし直してください。

既にSDKをインストールまたは展開してある場合は、フォルダーを指定できます。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Setup-BodyTracking.ps1 -SdkPath 'C:\Program Files\Azure Kinect Body Tracking SDK'
```

`-SdkPath` には `dnn_model_2_0_op11.onnx` のフルパスも指定できます。ダウンロードを使わず自動探索だけを行う場合は、`Setup-BodyTracking.ps1` を引数なしで実行します。モデルが見つからないときはエラーで終了します。

自動探索では、このプロジェクトと同じ階層の `Naispo_shadow-main`、`Naispo_shadow-Arima`、`Setup/BodyTracking`、プロジェクト内の `Setup/BodyTracking`、Program Files内のSDKフォルダーを調べます。SDKの `tools` と `sdk/windows-desktop/amd64/release/bin` にあるモデルを利用できます。

## 保存場所と検証

モデルの保存先は `Assets/StreamingAssets/AzureKinect/dnn_model_2_0_op11.onnx` です。モデルとUnityが生成する `.onnx.meta`、ダウンロード用の `Setup` フォルダーは `.gitignore` で除外しています。

スクリプトはモデルのサイズとSHA-256を、コピー前後に確認します。ダウンロードしたNuGetパッケージも、展開前に同じ方法で検証します。一致しないファイルは採用しません。アーカイブ内の既定のモデル1ファイルだけを取り出し、インストーラーやDLLは実行しません。

| ファイル | サイズ | SHA-256 |
| --- | ---: | --- |
| `dnn_model_2_0_op11.onnx` | 166,778,023 bytes | `40bf149c8e3dc4cba34cff4f5c3db6f5d5d4e6559d525df1aa02c2e6c80f7d2d` |
| `microsoft.azure.kinect.bodytracking.1.1.2.nupkg` | 198,516,805 bytes | `6151ff7e01a623fa2c15f01d9dfc22457c1353c484939d560a6f57a481ad83bf` |

同梱DLLの一覧、サイズ、SHA-256は `BODY_TRACKING_COPY_MANIFEST.json` に記録しています。このゲームではDirectMLで実行します。CUDA/TensorRT用の依存DLLは同梱していません。

## 配布元とライセンス

- [Microsoft公式SDK 1.1.2ダウンロードページ](https://www.microsoft.com/en-us/download/details.aspx?id=104221)
- [Microsoft公式NuGetパッケージ 1.1.2](https://www.nuget.org/packages/Microsoft.Azure.Kinect.BodyTracking/1.1.2)
- [スクリプトが取得するNuGetパッケージ](https://api.nuget.org/v3-flatcontainer/microsoft.azure.kinect.bodytracking/1.1.2/microsoft.azure.kinect.bodytracking.1.1.2.nupkg)

SDKに付属するライセンスと再配布対象の一覧、第三者の権利表示は `Documentation/Licenses/BodyTracking-LICENSE.txt`、`BodyTracking-REDIST.txt`、`BodyTracking-ThirdPartyNotices.txt` に保存しています。SDKの利用・配布にはこれらの条件が適用されます。
