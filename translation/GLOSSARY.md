# 用語集

[ja.xml](ja.xml) で使っている訳語の一覧です。訳語を揃えるために使います。書き方の決まりは [スタイルガイド](STYLE_GUIDE.md) を参照してください。

- 新しい訳を足すときは、ここにある訳語に合わせてください。
- 訳語を変えるときは、`ja.xml` の該当箇所すべてと、この用語集を一緒に直してください。
- この用語集に載せているのは、訳語を揃えるための用語と名称だけです。ゲームの文章(説明文など)は載せていません。

## ゲームの用語

| 英語 | 日本語 | 補足 |
|---|---|---|
| Life | ライフ | 医師として患者を治す側。モード名・キャンペーン名にも使う |
| Death | デス | 病気を操って犠牲者を死に至らせる側 |
| Campaign | キャンペーン | |
| Stage | ステージ | |
| Tutorial | チュートリアル | |
| Sandbox | サンドボックス | |
| Multiplayer | マルチプレイヤー | |
| Ranked | ランクマッチ | |
| Quick Match | クイックマッチ | |
| opponent | 対戦相手 | |
| patient | 患者 | ライフ側での呼び方 |
| victim | 犠牲者 | デス側での呼び方 |
| doctor | 医師 | |
| Bio Points | バイオポイント | |
| Bio Map | バイオマップ | 病気や治療を選ぶ画面 |
| Bio Coins | バイオコイン | |
| Stars | スター | |
| resources | リソース | ステージ中に集める対象(ウイルス、細菌、血球など) |
| harvest / collect | 回収する | |
| upgrade | アップグレード | |
| item | アイテム | バイオマップ上の項目 |
| node | ノード | |
| evolve | 取得する / 発症させる / 進化させる | 使い分けはスタイルガイドの5章 |
| system | 器官系 | |
| Overall Health | 総合健康度 | |
| Systems Health | 器官系の健康度 | |
| Health | 健康度 | 数値としての健康度 |
| terminate | 機能停止させる / 死亡させる | 器官系に対しては前者、犠牲者に対しては後者 |
| Systems Failure | 多臓器不全 | |
| Recovery | 回復 | |
| Recovery progress | 回復進行度 | |
| Diagnosis | 診断 | |
| Diagnostic Tests | 診断検査 | |
| Run Test | 検査する | |
| Test Results | 検査結果 | |
| Positive / Negative | 陽性 / 陰性 | |
| Treatments | 治療 | |
| Prescribe | 処方する | |
| Efficiency | 効果 | |
| Symptoms | 症状 | |
| Potential Symptoms | 起こりうる症状 | |
| Emergency Room | 救急救命室 | |
| Objective | 目標 | |
| Hint | ヒント | |
| Days / Days Left / Time Left | 日数 / 残り日数 / 残り時間 | |
| Score | スコア | |
| Difficulty | 難易度 | 軽度 / 中等度 / 重度 / 致死 の4段階 |
| Booster | スキル | ステージ前に選ぶ強化 |
| Booster Slot | スキルスロット | |
| XP Level | XPレベル | |
| Achievement | 実績 | |
| Autopsy Report | 検死報告書 | |
| Medical Report | 診療報告書 | |
| Time Of Death | 死亡時刻 | |
| Economy / Environment | 経済 / 環境 | スキルの分類 |
| Click / Tap | クリック | |
| Shop / Offer / Pack / Bundle | ショップ / オファー / パック / バンドル | |
| Energy | エナジー | |
| Season | シーズン | |
| Lobby / Room | ロビー / ルーム | |
| Hospital | 病院 | |

### 基本のボタン

| 英語 | 日本語 |
|---|---|
| Yes / No | はい / いいえ |
| OK / Cancel | OK / キャンセル |
| Next / Back / Close | 次へ / 戻る / 閉じる |
| Continue / Retry | 続ける / リトライ |
| Start / Quit | 開始 / 終了 |
| Options / Settings / Menu | オプション / 設定 / メニュー |
| Got It! | 了解！ |

## 名称

以下の表は、`ja.xml` のキーと訳語の対応です。左の列は、表の見出しに書いた接頭辞に続くキーの部分です。

キーはゲーム内部の識別子で、画面に表示される英語の名称とは異なるものがあります
(綴りの誤りを含むものや、後から名称だけが変更されたものがあるため)。その場合、訳は画面に表示される名称に合わせています。

### 器官系と性別

| キー(`Anatomy.` に続く部分) | 日本語 |
|---|---|
| `Circulatory` | 循環器系 |
| `Nervous` | 神経系 |
| `Respiratory` | 呼吸器系 |
| `Muscular` | 筋肉系 |
| `Digestive` | 消化器系 |
| `Renal` | 泌尿器系 |
| `Skeletal` | 骨格系 |
| `Immune` | 免疫系 |
| `Male` | 男性 |
| `Female` | 女性 |

### 項目の種類

バイオマップの分類名です。

| キー(`HealthConditionType.` に続く部分) | 日本語 |
|---|---|
| `Disease` | 病気 |
| `Lifestyle` | 生活習慣 |
| `RiskFactors` | 危険因子 |
| `IntensiveCare` | 集中治療 |
| `Recovery` | 回復 |
| `Treatment` | 治療 |
| `Diagnosis` | 診断 |

### 病気

| キー(`Condition.Name.` に続く部分) | 日本語 |
|---|---|
| `Addison disease` | アジソン病 |
| `Agina` | 狭心症 |
| `Alzheimer` | アルツハイマー病 |
| `Arrhythmia` | 不整脈 |
| `Arthritis` | 関節炎 |
| `Asthma` | 喘息 |
| `Blood clots` | 血栓 |
| `Bone death` | 骨壊死 |
| `Bone fractures` | 頻繁な骨折 |
| `Brain aneurysm` | 脳動脈瘤 |
| `Bronchitis` | 気管支炎 |
| `Burnout` | 燃え尽き症候群 |
| `Chronic bronchitis` | 慢性気管支炎 |
| `Chronic fatigue` | 慢性疲労 |
| `Chronic kidney failure` | 慢性腎不全 |
| `Chronic muscle cramp` | 慢性筋痙攣 |
| `Chronic stress` | 慢性ストレス |
| `Cirrhosis` | 肝硬変 |
| `Colon cancer` | 大腸がん |
| `Constipation` | 便秘 |
| `Crohn disease` | クローン病 |
| `Deep vein thrombosis` | 深部静脈血栓症 |
| `Dementia` | 認知症 |
| `Emphysema` | 肺気腫 |
| `Epileptic seizures` | てんかん発作 |
| `Fibromyalgia` | 線維筋痛症 |
| `Flu` | インフルエンザ |
| `Gallstones` | 胆石 |
| `Gastroenteritis` | 胃腸炎 |
| `H1N1` | H1N1 |
| `Heart attack` | 心臓発作 |
| `Hemiparesis` | 片麻痺 |
| `Hemorrhoids` | 痔 |
| `High cholesterol` | 高コレステロール |
| `Hypertension` | 高血圧 |
| `Immunodeficiency` | 免疫不全 |
| `Insomnia` | 不眠症 |
| `Irritable bowel` | 過敏性腸症候群 |
| `Kidney cysts` | 腎嚢胞 |
| `Kidney failure` | 腎不全 |
| `Kidney infection` | 腎盂腎炎 |
| `Kidney stones` | 腎結石 |
| `Leukemia` | 白血病 |
| `Lung Cancer` | 肺がん |
| `Lupus` | ループス |
| `Lupus nephritis` | ループス腎炎 |
| `Major depression` | 大うつ病 |
| `Muscular dystrophy` | 筋ジストロフィー |
| `Necrotizing fasciitis` | 壊死性筋膜炎 |
| `Nervous breakdown` | 神経衰弱 |
| `Osteoporosis` | 骨粗鬆症 |
| `Paget disease` | 骨パジェット病 |
| `Pancreatitis` | 膵炎 |
| `Parkinson` | パーキンソン病 |
| `Photosensitivity` | 光線過敏症 |
| `Pneumonia` | 肺炎 |
| `Pneumothorax` | 気胸 |
| `Postphlebitic syndrome` | 静脈炎後症候群 |
| `Potassium deficiency` | カリウム欠乏症 |
| `Pulmonary embolism` | 肺塞栓症 |
| `Respiratory failure` | 呼吸不全 |
| `Rickets` | くる病 |
| `Salmonella infection` | サルモネラ感染症 |
| `Sarcoidosis` | サルコイドーシス |
| `Sepsis` | 敗血症 |
| `Skin Cancer` | 皮膚がん |
| `Stroke` | 脳卒中 |
| `Tuberculosis` | 結核 |
| `Type 1 diabetes` | 1型糖尿病 |
| `Type 2 diabetes` | 2型糖尿病 |
| `Urinary Incontinence` | 尿失禁 |
| `Urinary track infection` | 尿路感染症 |
| `Abdominal aortic aneurysms` | 腹部大動脈瘤 |
| `Alport syndrome` | アルポート症候群 |
| `Androgenetic alopecia` | 男性型脱毛症 |
| `Anorexia nervosa` | 神経性やせ症 |
| `Breast cancer` | 乳がん |
| `Cervical cancer` | 子宮頸がん |
| `Haemophilia` | 血友病 |
| `Ovarian cancer` | 卵巣がん |
| `Polycystic ovary syndrome` | 多嚢胞性卵巣症候群 |
| `Prostate cancer` | 前立腺がん |
| `Scleroderma` | 強皮症 |
| `Testicular cancer` | 精巣がん |

### 危険因子

デス側で、病気を強める要因です。

| キー(`Condition.Name.` に続く部分) | 日本語 |
|---|---|
| `Booster.Alcoholic` | アルコール依存 |
| `Booster.Allergy` | アレルギー |
| `Booster.Dehydration` | 脱水 |
| `Booster.Hereditary` | 遺伝 |
| `Booster.JunkFoodAddict` | ジャンクフード中毒 |
| `Booster.Junkie` | 薬物依存 |
| `Booster.Malnutrition` | 栄養失調 |
| `Booster.MorbidlyObese` | 病的肥満 |
| `Booster.Obese` | 肥満 |
| `Booster.Over50` | 60歳以上 |
| `Booster.Overweight` | 過体重 |
| `Booster.Sedentary` | 運動不足 |
| `Booster.Smoker` | 喫煙者 |
| `Booster.Unlucky` | 不運 |
| `Booster.Workaholic` | 仕事中毒 |
| `Booster.Nymphomania` | 性依存症 |

### 回復を妨げるアイテム

デス側で、治療や回復を遅らせるためのアイテムです。

| キー(`Condition.Name.` に続く部分) | 日本語 |
|---|---|
| `Recovery.BadDiagnosis` | 誤診 |
| `Recovery.BadMedicine` | ヤブ医者 |
| `Recovery.BadMedicine2` | ヤブ医者 II |
| `Recovery.CountrySide` | 田舎暮らし |
| `Recovery.DoctorsOnStrike` | 医師のストライキ |
| `Recovery.Nosocomephobia` | 病院恐怖症 |
| `Recovery.Nosocomephobia2` | 病院恐怖症 II |
| `Recovery.NursesOnStrike` | 看護師のストライキ |
| `Recovery.Placebo` | ジェネリック治療 |
| `Recovery.Placebo2` | 治療の中断 |
| `Recovery.SurgeryError` | 手術ミス |

### 性感染症

| キー(`Condition.Name.` に続く部分) | 日本語 |
|---|---|
| `Std.Hpv` | HPV |
| `Std.Herpes` | 性器ヘルペス |
| `Std.Chlamydia` | クラミジア |
| `Std.Hiv` | エイズ |
| `Std.Syphilis` | 梅毒 |
| `Std.GenitalWarts` | 尖圭コンジローマ |
| `Std.Gonorrhea` | 淋病 |
| `Std.Hepatitis` | 肝炎 |
| `Std.None` | 性感染症なし |
| `Std.AlreadyExist` | 性感染症に感染済みです。 |

### 診断検査

| キー(`Condition.Name.` に続く部分) | 日本語 |
|---|---|
| `Abdominal CT Scan` | 腹部CT検査 |
| `ACTH stimulation test` | ACTH負荷試験 |
| `Allergy tests` | アレルギー検査 |
| `Bladder diary` | 排尿日誌 |
| `Blood calcium test` | 血中カルシウム検査 |
| `Blood Immunoglobulin test` | 血中免疫グロブリン検査 |
| `Bone biopsy` | 骨生検 |
| `Bone marrow test` | 骨髄生検 |
| `Bone Scan` | 骨シンチグラフィ |
| `Bones X-Rays` | 骨X線検査 |
| `Brain MRI` | 脳MRI |
| `Brain PET scan` | 脳PET検査 |
| `Cardiovascular Monitoring` | 心電図検査 |
| `Chest X-Ray` | 胸部X線検査 |
| `Colon tissue biospy` | 大腸生検 |
| `Colonoscopy` | 大腸内視鏡検査 |
| `Electroencephalogram` | 脳波検査 |
| `Exercise electrocardiogram` | 運動負荷心電図 |
| `Flu test` | インフルエンザ検査 |
| `FM-a Test` | FM/a検査 |
| `Glycated hemoglobin test` | 糖化ヘモグロビン検査 |
| `Infectious diseases blood panel` | 感染症血液検査 |
| `Kidney biopsy` | 腎生検 |
| `Kidney function tests` | 腎機能検査 |
| `Lung biopsy` | 肺生検 |
| `Lupus differential diagnosis` | ループスの鑑別診断 |
| `Metabolic panel` | 代謝パネル検査 |
| `Muscle biopsy` | 筋生検 |
| `Neurological exam` | 神経学的検査 |
| `Physical exam` | 身体診察 |
| `Psychiatrist consultation` | 精神医学的評価 |
| `Psychologist consultation` | 心理検査 |
| `Pulmonary angiogram` | 肺血管造影 |
| `Rectal exam` | 直腸診 |
| `Renal system MRI` | 泌尿器系MRI |
| `Respiratory monitoring` | 呼吸モニタリング |
| `Sigmoidoscopy` | S状結腸鏡検査 |
| `Skin biopsy` | 皮膚生検 |
| `Sleep monitoring` | 終夜睡眠ポリグラフ検査 |
| `Sphygmomanometer` | 血圧測定 |
| `Spirometer` | スパイロメーター |
| `Stethoscope exam` | 聴診 |
| `Stool sample test` | 便検査 |
| `Surgical exploration` | 試験的手術 |
| `Ultrasounds` | 超音波検査 |
| `Ultraviolet light testing` | 紫外線（UV）照射試験 |
| `Urinalysis` | 尿検査 |
| `Mammography` | マンモグラフィ |
| `Pap test` | パップテスト |
| `Pelvic exam` | 内診 |
| `Prostate biopsy` | 前立腺生検 |

### 治療

| キー(`Condition.Name.` に続く部分) | 日本語 |
|---|---|
| `Alcohol dependency therapy` | アルコール依存症治療 |
| `Alpha blocker` | α遮断薬 |
| `Amputation` | 切断術 |
| `Analgesics` | 鎮痛薬 |
| `Antibiotics` | 抗生物質 |
| `Anticholinergics` | 抗コリン薬 |
| `Antidepressants` | 抗うつ薬 |
| `Anti-diarrheals` | 下痢止め |
| `Anti-epileptic medication` | 抗けいれん薬 |
| `Anti-inflammatory drugs` | 抗炎症薬 |
| `Antiviral drugs` | 抗ウイルス薬 |
| `Anxiolytics` | 抗不安薬 |
| `Aspirin` | アスピリン |
| `Atorvastatin (Statins)` | スタチン |
| `Bisphosphonates` | ビスホスホネート |
| `Bladder training` | 膀胱訓練 |
| `Blood thinners` | 抗凝固薬 |
| `Bronchodilators` | 気管支拡張薬 |
| `Calcium-Vitamin D Supplements` | カルシウム/ビタミンD補給 |
| `Carbidopa-levodopa` | カルビドパ/レボドパ |
| `Cease work` | 長期休職 |
| `Chemotherapy` | 化学療法 |
| `Chest tube insertion` | 胸腔ドレナージ |
| `Cholecystectomy` | 胆嚢摘出術 |
| `Cholinesterase inhibitors` | コリンエステラーゼ阻害薬 |
| `Clotbusters` | 血栓溶解薬 |
| `Corticosteroids` | 副腎皮質ステロイド |
| `Cyst Surgery` | 嚢胞手術 |
| `Dialysis` | 血液透析 |
| `Digestive tract surgery` | 消化管手術 |
| `Diuretics` | 利尿薬 |
| `Excisional surgery` | 切除手術 |
| `Fiber supplements` | 食物繊維サプリメント |
| `Immunoglobulin therapy` | 免疫グロブリン療法 |
| `Immunosuppressant drugs` | 免疫抑制薬 |
| `Inhaled corticosteroids` | 吸入ステロイド |
| `Insulin` | インスリン注射 |
| `Intravenous antibiotics` | 点滴抗生物質 |
| `Isoniazid` | イソニアジド |
| `Kidney transplant` | 腎移植 |
| `Liver transplant` | 肝移植 |
| `Mechanical ventilation` | 人工呼吸 |
| `Nitrates` | ニトログリセリン |
| `Pacemaker` | ペースメーカー |
| `Pancreas surgery` | 膵臓手術 |
| `Pancreatic enzyme supplements` | 膵酵素補充薬 |
| `Partial colectomy` | 腸切除術 |
| `Physiotherapy` | 理学療法 |
| `Plaster Care` | ギプス固定 |
| `Postphlebitic therapy` | 静脈炎後療法 |
| `Potassium supplements` | カリウム補充 |
| `Psychiatrist counseling` | 精神科カウンセリング |
| `Pulmonary rehabilitation` | 呼吸リハビリテーション |
| `Relaxation technique` | リラクゼーション法 |
| `Rest` | 安静 |
| `Rubber band ligation` | ゴム輪結紮術 |
| `Selective serotonin reuptake inhibitors` | 選択的セロトニン再取り込み阻害薬 |
| `Sleeping pills` | 睡眠薬 |
| `Stress management therapy` | ストレス管理療法 |
| `Strict diet` | 厳格な食事療法 |
| `Supplemental oxygen` | 酸素療法 |
| `Surgical clipping` | クリッピング術 |
| `Temporary hemodialysis` | 一時的血液透析 |
| `Thrombolytic` | 血栓溶解療法 |
| `Wedge resection` | 楔状切除術 |
| `ACE inhibitors` | ACE阻害薬 |
| `Antifibrinolytics` | 抗線溶薬 |
| `Birth control pills` | 経口避妊薬 |
| `Endovascular surgery` | 血管内手術 |
| `Hysterectomy` | 腟式子宮全摘術 |
| `Mastectomy` | 乳房切除術 |
| `Minoxidil` | ミノキシジル |
| `Ovarian ablation` | 卵巣機能抑制 |
| `Radiation therapy` | 放射線療法 |
| `Radical inguinal orchiectomy` | 精巣摘除術 |
| `Radical prostatectomy` | 前立腺全摘除術 |

### 生活習慣

ライフ側で、患者の健康を支える習慣です。

| キー(`Condition.Name.` に続く部分) | 日本語 |
|---|---|
| `Aerobic exercise` | 有酸素運動 |
| `Athlete` | アスリート |
| `Drug free` | 薬物を使わない生活 |
| `Fitness enthusiast` | フィットネス愛好家 |
| `Good genetics` | 優れた遺伝子 |
| `Healthy diet` | 健康的な食事 |
| `Meditation` | 瞑想 |
| `Mediterranean diet` | 地中海式食事 |
| `Moderate drinking` | 節度ある飲酒 |
| `Proper Hydration` | 適切な水分補給 |
| `Relaxation training` | リラクゼーション療法 |
| `Sleep routine` | 規則正しい睡眠 |
| `Smoke Free` | 禁煙 |
| `Strength training` | 筋力トレーニング |
| `Weight loss program` | 減量食 |
| `Yoga` | ヨガ |

### 集中治療

ライフ側で、診断や治療を強化するアイテムと手術です。

| キー(`Condition.Name.` に続く部分) | 日本語 |
|---|---|
| `Anabolic therapy` | アナボリックステロイド療法 |
| `Better lifestyles` | 生活習慣の改善 |
| `Better treatments` | 治療の改善 |
| `Fast diagnosis` | 迅速診断 |
| `Fast treatments` | 迅速治療 |
| `Gastric bypass` | 胃バイパス手術 |
| `Bone graft` | 骨移植 |
| `Heart bypass surgery` | 心臓移植 |
| `Kidney transplantation` | 腎臓移植 |
| `Lung transplant` | 肺移植 |
| `Neuroregeneration` | 神経再生 |
| `Plasmapheresis` | 血漿交換 |

### 症状

| キー(`Symptom.Name.` に続く部分) | 日本語 |
|---|---|
| `Diziness` | めまい |
| `Chest pain` | 胸痛 |
| `Nausea` | 吐き気 |
| `Fatigue` | 疲労感 |
| `Shortness of breath` | 息切れ |
| `Sweating` | 発汗 |
| `Irregular heartbeat` | 不規則な心拍 |
| `Blurred vision` | かすみ目 |
| `Headache` | 頭痛 |
| `Light sensitivity` | 光過敏 |
| `Vomiting` | 嘔吐 |
| `Arm Pain` | 腕の痛み |
| `Stomach Pain` | 胃痛 |
| `Confusion` | 錯乱 |
| `Leg Pain` | 脚の痛み |
| `Cough` | 咳 |
| `Fever` | 発熱 |
| `Coughing up blood` | 喀血 |
| `Chronic cough` | 慢性的な咳 |
| `Runny nose (Mucus)` | 鼻水 |
| `Muscles Aches` | 筋肉痛 |
| `Sore throat` | 喉の痛み |
| `Anxiety` | 不安 |
| `Mood swings` | 気分の変動 |
| `Paranoia` | 被害妄想 |
| `Personality changes` | 人格変化 |
| `Memory loss` | 記憶障害 |
| `Shaking` | 震え |
| `Paralysis` | 麻痺 |
| `Loss of reflexes` | 反射消失 |
| `Muscle weakness` | 筋力低下 |
| `Skin rash` | 発疹 |
| `Joint pain` | 関節痛 |
| `Back pain` | 背中の痛み |
| `Swelling-bruising` | 腫れ/あざ |
| `Hard stools` | 硬い便 |
| `Blood in stools` | 血便 |
| `Abdominal pain` | 腹痛 |
| `Gas` | ガス |
| `Diarrhea` | 下痢 |
| `Frequent urination` | 頻尿 |
| `Excessive thirst` | 強い喉の渇き |
| `Incontinence` | 失禁 |
| `Urination difficulty` | 排尿困難 |
| `Elevated heart rate` | 心拍数上昇 |
| `Legs swelling` | 脚のむくみ |
| `Frequent infections` | 頻繁な感染症 |
| `Weight loss` | 体重減少 |
| `Blood in urine` | 血尿 |
| `Hearing loss` | 難聴 |
| `Hair Loss` | 脱毛 |
| `Groin pain` | 鼠径部痛 |
| `Acid reflux` | 胃酸逆流 |
| `Pelvic pain` | 骨盤痛 |
| `Menstrual irregularities` | 月経不順 |
| `Weight gain` | 体重増加 |

### ステージ名

| キー(`StageInfo.Name.` に続く部分) | 日本語 |
|---|---|
| `KillStageTest` | デスステージ テスト |
| `CureStageTest` | ライフステージ テスト |
| `KillHeadStart` | ヘッドスタート |
| `KillYoungAndHealthy` | 若くて健康 |
| `KillRace1` | デスレース |
| `KillTimeBomb` | 時限爆弾 |
| `KillTotalFailure` | 全機能不全 |
| `KillCardiorespiratoryArrest` | 心肺停止 |
| `KillDeadline` | デッドライン |
| `KillPerfectLifestyle` | 完璧な生活習慣 |
| `KillRoadblock` | ロードブロック |
| `CureDiagnosisMd` | 診断医 |
| `CureResidency` | レジデント |
| `CureInternship` | インターン |
| `CureMdWar1` | 生物医学戦 |
| `CureMdWar2` | 生物医学戦 2 |
| `CureTerminalPatient` | 末期患者 |
| `CureAsymptomatic` | 無症状 |
| `CureTimeBomb` | 時限爆弾 |
| `CureRemission` | 寛解 |
| `MultiplayerArenaKill` | マルチプレイヤーアリーナ デスモード |
| `MultiplayerArenaCure` | マルチプレイヤーアリーナ ライフモード |
| `KillRace2` | デスレース II |
| `KillMultitask` | マルチキル |
| `KillShield` | デスシールド |
| `CureEnigma` | エニグマ |
| `CureShield` | ライフシールド |
| `CureMultitask` | マルチタスク |
