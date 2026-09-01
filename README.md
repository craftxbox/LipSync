# LipSync
Beatsaber mod, works in tandem with CustomAvatars to add lipsyncing to avatars. uses the OpenLipSync library from Resonite.  
  
Models need to have Blendshapes roughly named like the following for lipsyncing to work:  
  
viseme_sil  
viseme-sil  
viseme.sil  
visemesil  
sil  
  
It is case insensitive.  
  
The full list of required visemes is:  
  
SIL  
PP  
FF  
TH  
DD  
KK  
CH  
SS  
NN  
RR  
AA (A)  
EE (E)  
IH (I)  
OH (U)  
OU (U)  
  
There is no support for laughter visemes. See #1 for information.  
  
## Dependencies
- [CustomAvatars](https://github.com/nicoco007/BeatSaberCustomAvatars)
- [Microsoft.ML.OnnxRuntime](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime/)