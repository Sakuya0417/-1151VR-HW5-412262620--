1151 VR 作業五
Unity 3D 寶可夢收服場景
1. 專案截圖
未執行前的場景：左側為訓練家、右側為寶可夢，背景為起伏的山脈、樹林與草地。
 <img width="938" height="528" alt="image" src="https://github.com/user-attachments/assets/86156975-79bc-45f4-8831-1615cdf9002b" />

圖 1　未執行前的場景
執行過程關鍵畫面（擷取自錄影）：
 
圖 2　訓練家舉手蓄力，準備拋球	 
<img width="469" height="264" alt="image" src="https://github.com/user-attachments/assets/3bee6f7b-92fe-4b7e-8595-c20d34719cc9" />

圖 3　精靈球由左至右拋物線飛行
 <img width="469" height="264" alt="image" src="https://github.com/user-attachments/assets/3a78ad41-3c8c-4d99-a145-d56cb044fb1c" />

圖 4　命中後紅光將寶可夢吸入	 
<img width="469" height="264" alt="image" src="https://github.com/user-attachments/assets/c5b3fea6-0a03-433c-bae9-1b80d3bb41a5" />

圖 5　精靈球落地搖晃（每次搖晃有音效）
 <img width="469" height="264" alt="image" src="https://github.com/user-attachments/assets/3310172e-9fe6-40ad-8c71-d90f3d594043" />

圖 6　收服成功：星星特效、勝利音樂與「Gotcha! 收服成功！」
<img width="938" height="528" alt="image" src="https://github.com/user-attachments/assets/251961b8-dc29-4d68-a6da-c77c87a1b560" />

2. GitHub 連結
https://github.com/Sakuya0417/-1151VR-HW5-412262620--.git
3. YouTube 連結
https://youtu.be/xZ31KMJVzdw
4. 製作流程與相關操作
4.1 建立 Unity 3D 專案
1.	開啟 Unity Hub → 新專案 → 選擇 Universal 3D 範本。
2.	專案名稱輸入 1151VR-HW5，選擇「建立新本機專案」，位置 C:\Users\93102，按 建立專案。
3.	將自製腳本放入 Assets/HW5/（Scripts 為執行時腳本，Editor 為編輯器工具）。
4.2 訓練家拋球手臂動作（TrainerThrowAnimator.cs）
以程序化動畫在 LateUpdate 轉動右上臂與前臂骨骼，任何人形模型都適用，不需要另外製作動畫檔。
階段	時間	上臂角度	手肘
蓄力（舉手後拉）	0.50 秒	0° → 100°（向後）	彎曲 80°
揮臂（加速甩出）	0.28 秒	100° → 280°（過頭頂到前方）	打直，約 80% 時放開球
隨球動作	0.45 秒	280° → 330°	微彎
回位	0.50 秒	330° → 360°（下垂）	0°

出手前，精靈球會固定在訓練家右手掌上（位置換算成手骨骼的區域座標），所以揮臂過程中球會跟著手一起移動。
4.3 精靈球飛行與收服特效（PokeballThrowController.cs、HW5FX.cs）
1.	飛行（由左至右）：二次拋物線 p = Lerp(起點, 終點, t) + 上方 × 高度 × 4t(1−t)，飛行 1 秒，球會旋轉並拖出白色拖尾。
2.	命中：白色爆散粒子＋光環衝擊波，球彈回半空。
3.	吸入：紅色光束連到寶可夢，寶可夢變紅發光、縮小並被吸進球裡。
4.	落地並搖晃 3 次：每次搖晃後冒出小星光。
5.	收服成功：金色星星爆發＋光環＋持續的星星噴泉，畫面中央顯示「Gotcha! 收服成功！」。
所有粒子特效都由程式即時產生（Particle System、Trail Renderer、Line Renderer），不需要外部素材。
4.4 音效（HW5Audio.cs）
時機	音效	合成方式
出手	呼嘯聲	低通濾波白雜訊，音量先升後降
命中	「啵」	900 → 180 Hz 下滑正弦波＋指數衰減
紅光吸入	上升音	250 → 1400 Hz 滑音＋顫音
落地	「咚」	110 Hz 低音＋雜訊
每次搖晃	「喀啦—喀」	兩下雜訊脈衝＋碰撞低音＋外殼共鳴
收服成功	上鎖聲＋勝利音樂	喀嚓脈衝，接著 C5–E5–G5–C6 琶音與 C 大和弦

4.5 操作方式
操作	功能
按 Play	1.5 秒後自動拋球一次
Space	再拋一次球
R	重置（寶可夢與球回到原位）
F12 / P	執行中截圖，存到 Screenshots/
HW5 ▸ Capture Preview Screenshot	拍攝未執行前的場景截圖（1920×1080）
HW5 ▸ Record Video	以固定 30 fps 錄下完整演出（含音效）

在 GameManager 的 Inspector 可調整：飛行時間、拋物線高度、搖晃次數、收服光顏色、握球位置（Hold Offset）、音效音量。
4.6 模型來源與授權（Credits）
•	訓練家：Mixamo（Adobe）角色「Remy」。
•	寶可夢：Sketchfab「Pikachu」，上傳者 gaddiellartey2010（CC Attribution）；原始模型檔由 Random Talking Bush 提供，Roxas358 製作貼圖。
•	精靈球：Sketchfab「Pokemon Basic Pokeball」，作者 JMGameDev（CC Attribution）。
•	Pokémon 為 The Pokémon Company／Nintendo／Game Freak 之商標，本專案僅供課程作業使用。
