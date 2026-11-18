# Animated office characters

ตัวละครครบ 9 ตัว: Female_01–03, Male_01, Male_03–07

แต่ละโฟลเดอร์มี prefab พร้อม CapsuleCollider, NPCQueueAgent, NPCCharacterAnimation,
generic skeleton 19 bones, SkinnedMeshRenderer, Animator Controller และ 11 clips:

| Motion | ท่า |
| --- | --- |
| Idle | ยืนรอ หายใจ ขยับตัวเล็กน้อย |
| Walk | เดิน แกว่งแขนและขา |
| Grabbed | ดิ้นขณะถูกจับยก |
| Falling | กางแขน เตะขาขณะปล่อยตก |
| Land | ย่อตัวรับพื้นแล้วคืนท่ายืน |
| HeadShake | สะบัดหัว |
| ArmFlick | สะบัดแขน/ข้อมือ |
| LegShake | เขย่าขา/เท้า |
| LookAround | หันมองซ้ายขวา |
| Stretch | ยืดแขนและตัว |
| Shrug | ยักไหล่ เอียงหัว |

## ใช้งาน

- ฉาก `Assets/TheElevatorLobby/Scenes/Elevator_Lobby_Main.unity` ตั้ง spawn pool ให้ใช้ prefab ทั้ง 9 ตัวแล้ว
- เปิด `Character_Animation_Preview.unity` ในโฟลเดอร์นี้แล้วกด Play เพื่อดูทุกท่าหมุนวน
- ใช้ prefab `ชื่อ_Animated.prefab` เมื่อต้องการนำตัวละครไปวางเอง
- เดินตามการเคลื่อนที่จริงของ root; ไม่ใช้ root motion
- ขณะหยุดยืน สุ่มท่าทุก 3.5–8 วินาที แต่ละตัวมีท่าที่ชอบและสุ่มความเร็ว 0.91–1.09
- NPCQueueAgent.SetGrabbed(true) เริ่มท่าถูกจับ; NPCGrabController เรียก ReleaseToGround ตอนปล่อย แล้ว NPCQueueAgent ควบคุมการลดระดับและเรียก Land เมื่อถึงพื้น
- NPCQueueManager จำกัดคิวด้วย maxNPCs และจำกัด NPC ทั้งหมดที่สร้างไว้ด้วย maxSpawnedNPCs (ค่าเริ่มต้น 24)
- ระบบปล่อยยังใช้ความเร็วลดระดับเดิมของเกม ไม่ได้เปลี่ยนเป็น ragdoll/ฟิสิกส์ตกอิสระ

## แก้ไขและสร้างใหม่

`Assets/Script/Editor/CharacterRigBuilder.cs` เก็บตำแหน่ง joints และการสร้าง skin weights/clips
เรียกเมนู `Tools > The Elevator > Characters` เพื่อสร้างใหม่หรือเชื่อม spawn pool
การสร้างใหม่จะเขียนทับเฉพาะ assets ที่สร้างในโฟลเดอร์ Animated นี้

ไฟล์ FBX ต้นฉบับไม่ถูกเขียนทับ ชุดนี้ใช้ generic rig ใน Unity ไม่ใช่ Humanoid Avatar
ไม่มี finger/facial bones และไม่มี ragdoll เนื้อผ้าและผมใช้ bone weights ตามตัวละคร
ใช้ material แยกเพื่อแก้ texture ที่อ้างผิดตัวและปัญหาจาก normal maps ของไฟล์ต้นฉบับ

โมเดลความละเอียดสูงหลายตัวมีประมาณ 0.3–0.85 ล้าน vertices ต่อตัว ยังไม่ได้ทำ LOD/ลด polygon
ควรทำ retopology/LOD เพิ่มหากจะมี NPC จำนวนมากหรือใช้บนมือถือ

## ตรวจสอบแล้ว

- 9 prefabs มี 19 bones และ 11 states/11 clips ครบ
- skin weights ครบทุก vertex ผลรวมเท่ากับ 1 ไม่มี animation binding หาย
- ทดสอบ Play Mode: เดิน, ถูกจับ, ตก, ลงพื้น และ random gesture ผ่านทั้ง 9 ตัว
- ทดสอบ NPCGrabController.ReleaseNPC ลงถึงพื้นและกลับ Idle ได้
- ตรวจภาพจาก mesh ที่ skin แล้วในท่ายืน เดิน ยก ตก ลงพื้น และสะบัดหัว
- ตรวจ Console หลังทดสอบ ไม่พบ error/warning
