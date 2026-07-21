# MiniatureGarden SkillCore Adapters

杩欎竴灞備笉鏄?`SkillCore` 妗嗘灦鏍稿績鏈綋锛岃€屾槸褰撳墠椤圭洰鎶?`SkillCore` 鎺ュ埌鍏蜂綋瑙掕壊涓氬姟涓婄殑閫傞厤灞傘€?
## 褰撳墠鐘舵€?
- `ProjectAssets/`
  - 杩欐槸褰撳墠椤圭洰琛屼负閰嶇疆璧勪骇涓庢暟鍊艰祫浜х殑姝ｅ紡鐩綍
  - 渚嬪 `UnitAssetInformation`銆乣UnitAbilityNumericProfile`
  - 涔熸壙鎺ュ綋鍓嶉」鐩殑绛栫暐璧勪骇鍩虹被锛屼緥濡?`UnitConditionSourceAsset`銆乣UnitTransitionPolicyAsset`銆乣UnitAttackResolverAsset`

- `Runtime/`
  - 杩欐槸褰撳墠椤圭洰渚ц繍琛屾椂妗ユ帴浠ｇ爜鐨勬寮忕洰褰?  - 褰撳墠閫昏緫鑱岃矗宸茬粡鍖哄垎涓衡€滄ˉ鎺?/ 瑙勫垯 / 鍏变韩鏁版嵁鈥濅笁绫伙紝浣嗙墿鐞嗙洰褰曚粛淇濇寔鍗曞眰锛岄伩鍏嶅啀娆¤Е鍙?Unity 宸ョ▼鍚屾闂

褰撳墠杩愯鏃跺崟灞傜洰褰曞唴鐨勮亴璐ｅ垝鍒嗭細

- 妗ユ帴绫?  - `CharacterBehaviorEventReceiver`
  - `CharacterBehaviorRuntime`
  - `CharacterTargetingUtility`
  - `ICharacterBehaviorRequester`

- 瑙勫垯绫?  - `ICharacterAttackResolver`
  - `ICharacterTransitionPolicy`
  - `ICharacterConditionSource`
  - `DefaultCharacterAttackResolver`
  - `DefaultCharacterTransitionPolicy`

- 鍏变韩鏁版嵁绫?  - `CharacterAttackPlayRequest`
  - `CharacterAttackPlaybackStage`
  - `CharacterTransitionRequest`
  - `CharacterStateId`
  - `CharacterStance`

褰撳墠鐩綍鐨勪綔鐢ㄦ槸锛?
- 鏄庣‘椤圭洰渚ц涓鸿祫浜у畾涔変笌 `SkillCore` 妗嗘灦鏍稿績鐨勫垎灞?- 鏄庣‘椤圭洰渚?`SkillCore` 閫傞厤灞傚湪鏋舵瀯涓婄殑鐙珛杈圭晫
- 璁╄繍琛屾椂妗ユ帴涓庣紪杈戝櫒閫傞厤鍦ㄧ墿鐞嗙洰褰曚笂淇濇寔涓€鑷?- 闃叉琛屼负绯荤粺妗ユ帴浠ｇ爜缁х画鍥炴祦鍒?`Character` 杩愯鏃朵富鐩綍锛屼繚鎸佽鑹查┍鍔ㄥ眰涓庤涓洪€傞厤灞傚垎绂?- 褰撳墠杩涗竴姝ョ害瀹氾細鍑℃槸鈥滆В閲婃煇涓涓鸿鎬庝箞鎾€佸浣曞垏鎹€佸浣曟瀯閫犺涓鸿姹傗€濈殑妗ユ帴绫诲瀷锛屼紭鍏堟斁鍦?`SkillCore/Runtime`锛岃€屼笉鏄户缁暎钀藉湪 `Character` 鐩綍
- 鍚屾椂锛屸€滆涓鸿姹傚叆鍙ｆ帴鍙ｂ€濆拰鈥滆鑹插埌 BehaviorInterpreter 鐨勮繍琛屾椂妗ユ帴鍣ㄢ€濅篃鏀跺彛鍒?`SkillCore/Runtime`锛岄伩鍏?`Character/Core` 鍚屾椂鎵挎媴涓帶鍜岃涓烘ˉ鎺ヤ袱灞傝亴璐?- 瑙掕壊涓撳睘 `ScriptableObject` 绛栫暐璧勪骇鍒欑粺涓€鐣欏湪 `SkillCore/ProjectAssets`锛岄伩鍏?`Runtime` 鐩綍鍚屾椂娣锋斁妗ユ帴閫昏緫涓庨」鐩厤缃祫浜у熀绫?- 褰撳墠 `Runtime` 鐨勮亴璐ｅ凡缁忔寜妗ユ帴銆佽鍒欍€佸叡浜暟鎹笁绫绘敹鍙ｏ紝浣嗕负浜嗕繚鎸佸伐绋嬬ǔ瀹氾紝鐪熷疄鐗╃悊鐩綍浠嶆槸鍗曞眰

## 涓?Framework 鐨勬帴鍙ｇ害瀹?
褰撳墠椤圭洰浠嶅湪浣跨敤涓€閮ㄥ垎鏃х殑 `Character*` 鍏煎鎺ュ彛锛屼絾妗嗘灦灞傚凡缁忓紑濮嬫彁渚涙洿涓€х殑鍏ュ彛锛?
- 鍗曚綅闈欐€佸畾涔変紭鍏堜緷璧?`IUnitDefinition`
- 鍗曚綅琛屼负閰嶇疆鑱氬悎鍙ｄ紭鍏堜緷璧?`IUnitBehaviorDefinition`
- 鍗曚綅绱㈡晫浼樺厛渚濊禆 `IUnitTargetingProvider`
- 闃佃惀浼樺厛渚濊禆 `UnitAlignment`

鏈洰褰曚粛淇濈暀 `UnitAssetInformation`銆乣IUnitRuntimeDefinition` 杩欑被鍛藉悕锛屾槸鍥犱负瀹冧滑灞炰簬褰撳墠椤圭洰璧勪骇璇箟锛屼笉绛変簬妗嗘灦鏈潵蹇呴』缁х画娌跨敤杩欏鍛藉悕銆?
琛ュ厖璇存槑锛?
- `IUnitRuntimeDefinition` 鐜板湪搴旇涓哄綋鍓嶉」鐩墿灞曟帴鍙?- 瀹冪殑妗嗘灦鏈€灏忓叕鍏卞熀绾挎槸 `IUnitBehaviorDefinition`
- 鏂伴」鐩嫢涓嶉渶瑕佸綋鍓嶉」鐩繖濂楃瓥鐣ヨ祫浜х粨鏋勶紝鍙疄鐜?`IUnitBehaviorDefinition` 灏辫冻澶熸妸琛屼负绯荤粺鎺ヨ捣鏉?
## 杈圭晫瑙勫垯

杩欓噷鎵胯浇鐨勯」鐩晶閫傞厤灞傚彲浠ヤ緷璧栵細

- `Framework/SkillCore`
- `Framework/RPGGameplay`
- `Business/MiniatureGarden/SkillCore/ProjectAssets`
- `Business/MiniatureGarden/Character`
- `Business/MiniatureGarden/Camera`

浣?`Framework/SkillCore` 涓嶅簲璇ュ弽鍚戜緷璧栬繖灞傘€?
## 鍚庣画缁х画鎷嗗垎鐨勬柟鍚?
濡傛灉鍚庨潰瑕佹妸鏁村琛屼负缂栬緫鍣ㄦ洿瀹屾暣鍦板崟鎷庡嚭鏉ワ紝浼樺厛缁х画妫€鏌ヨ繖浜涚偣锛?
1. 褰撳墠椤圭洰琛屼负鏁板€艰祫浜?`UnitAbilityNumericProfile`
2. 褰撳墠椤圭洰琛屼负閰嶇疆璧勪骇 `UnitAssetInformation`
3. 褰撳墠椤圭洰鐨勮涓虹姸鎬佹満涓庤姹傜瓥鐣?4. 褰撳墠椤圭洰鐩告満銆侀煶棰戙€乂FX銆丳rojectile 鐨勬ˉ鎺ラ€昏緫

鍘熷垯锛?
- 鍙鐢ㄧ殑浣滆€呮湡涓庤繍琛屾椂鏍稿績锛岀户缁暀鍦?`Framework/SkillCore`
- 閫氱敤 RPG 鎴樻枟鎵胯浇鑳藉姏锛岀暀鍦?`Framework/RPGGameplay`
- 鍙睘浜庡綋鍓嶉」鐩鍒欑殑妗ユ帴涓庨€傞厤锛岀暀鍦ㄦ湰鐩綍
