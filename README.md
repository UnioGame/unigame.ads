# UniGame Ads Module

## Overview

UniGame Ads Module provides a unified advertising system for Unity games with support for multiple ad networks. The module features:


## Installation

Add the package to your Unity project using the Package Manager:

### Dependencies

```json
{
  "dependencies": {
    "com.unity.addressables": "2.6.0",
    "com.unigame.addressablestools" : "https://github.com/UnioGame/unigame.addressables",
    "com.unigame.unicore": "https://github.com/UnioGame/unigame.core.git",
    "com.unigame.rx": "https://github.com/UnioGame/unigame.rx.git",
    "com.cysharp.unitask" : "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
    "com.cysharp.r3": "https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity",
    "com.github-glitchenzo.nugetforunity": "https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity"
  }
}
```

- NuGetForUnity installation reference: https://github.com/GlitchEnzo/NuGetForUnity

- R3 installation is required for the module to work properly: https://github.com/Cysharp/R3


## Configuration

- Installation config

```csharp
[CreateAssetMenu(menuName = "UniGame/Ads/Ads Service Source", fileName = "Ads Service Source")]
public class GameAdsServiceSource : DataSourceAsset<IAdsService>
```


- Placements configuration

```csharp
[CreateAssetMenu(menuName = "UniGame/Ads/Configuration", fileName = "AdsConfiguration")]
public class AdsConfigurationAsset : ScriptableObject
```

Any custom ads platform can be added by implementing the `IAdsService` 
interface and creating a corresponding `AdsProvider`.

Few providers are already implemented:
- LevelPlay
- Yandex Ads
- Google AdMob


# Yandex Ads

https://github.com/yandexmobile/yandex-ads-unity-plugin

Go to + -> Install package from git URL... and paste 

https://github.com/yandexmobile/yandex-ads-unity-plugin.git?path=/mobileads-sdk (replace mobileads-sdk with the name of the desired package)


# Google Ad Mob

https://github.com/googleads/googleads-mobile-unity

To enable Google AdMob support add scriptable define "ADMOB_ENABLED" to you project.


# LevelPlay

https://docs.unity3d.com/Packages/com.unity.services.levelplay@8.9/manual/index.html