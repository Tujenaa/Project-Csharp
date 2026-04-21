USE TourGuideDB;

-- ========================
-- LANGUAGES
-- ========================
CREATE TABLE Languages (
    Id         INT PRIMARY KEY IDENTITY(1,1),
    Code       NVARCHAR(10)  NOT NULL UNIQUE,   -- 'vi', 'en', 'ja', 'zh', ...
    Name       NVARCHAR(100) NOT NULL,           -- 'Tiếng Việt', 'English', ...
    IsActive   BIT           NOT NULL DEFAULT 1,
    OrderIndex INT           NOT NULL DEFAULT 0  -- thứ tự hiển thị trên UI
);

-- ========================
-- USERS
-- ========================
CREATE TABLE Users (
    Id           INT PRIMARY KEY IDENTITY(1,1),
    Username     NVARCHAR(100),
    PasswordHash NVARCHAR(255),

    Name         NVARCHAR(150),
    Email        NVARCHAR(150),
    Phone        NVARCHAR(20),

    Role NVARCHAR(50) CHECK (Role IN ('ADMIN', 'OWNER', 'CUSTOMER'))
);

-- ========================
-- POI
-- ========================
CREATE TABLE POI (
    Id           INT PRIMARY KEY IDENTITY(1,1),
    Name         NVARCHAR(255),
    Description  NVARCHAR(MAX),
    Address      NVARCHAR(255),

    Phone        NVARCHAR(20),

    Latitude     FLOAT,
    Longitude    FLOAT,
    Radius       INT,

    Status NVARCHAR(20)
        CHECK (Status IN ('PENDING', 'APPROVED', 'REJECTED'))
        DEFAULT 'APPROVED',
    RejectReason NVARCHAR(500) NULL,

    OwnerId INT,
    FOREIGN KEY (OwnerId) REFERENCES Users(Id)
);

-- ========================
-- AUDIO
-- Mỗi dòng = 1 cặp (POI, Language)
-- Thêm ngôn ngữ mới chỉ cần INSERT vào Languages rồi INSERT vào Audio,
-- không cần ALTER TABLE.
-- ========================
CREATE TABLE Audio (
    Id         INT PRIMARY KEY IDENTITY(1,1),
    PoiId      INT NOT NULL,
    LanguageId INT NOT NULL,
    Script    NVARCHAR(MAX) NOT NULL,

    FOREIGN KEY (PoiId)      REFERENCES POI(Id)       ON DELETE CASCADE,
    FOREIGN KEY (LanguageId) REFERENCES Languages(Id),

    CONSTRAINT UQ_Audio_Poi_Lang UNIQUE (PoiId, LanguageId)
);

-- ========================
-- HISTORY
-- ========================
CREATE TABLE History (
    Id             INT PRIMARY KEY IDENTITY(1,1),
    PoiId          INT,
    UserId         INT,

    PlayTime       DATETIME DEFAULT GETDATE(),
    ListenDuration INT NOT NULL DEFAULT 0,

    FOREIGN KEY (PoiId)  REFERENCES POI(Id),
    FOREIGN KEY (UserId) REFERENCES Users(Id)
);

-- ========================
-- POI IMAGES
-- ========================
CREATE TABLE POIImages (
    Id          INT PRIMARY KEY IDENTITY(1,1),
    PoiId       INT NOT NULL,
    ImageUrl    NVARCHAR(500) NOT NULL,
    IsThumbnail BIT DEFAULT 0,

    CreatedAt   DATETIME DEFAULT GETDATE(),

    FOREIGN KEY (PoiId) REFERENCES POI(Id)
);

-- ========================
-- TOURS
-- ========================
CREATE TABLE Tours (
    Id           INT PRIMARY KEY IDENTITY(1,1),
    Name         NVARCHAR(255) NOT NULL,
    Description  NVARCHAR(MAX),
    ThumbnailUrl NVARCHAR(500) NULL,

    Status NVARCHAR(20)
        CHECK (Status IN ('DRAFT', 'PUBLISHED', 'ARCHIVED'))
        DEFAULT 'DRAFT',

    CreatedBy INT NOT NULL,
    CreatedAt DATETIME DEFAULT GETDATE(),
    UpdatedAt DATETIME DEFAULT GETDATE(),

    FOREIGN KEY (CreatedBy) REFERENCES Users(Id)
);

-- ========================
-- TOUR - POI (nhiều-nhiều)
-- ========================
CREATE TABLE TourPOI (
    Id         INT PRIMARY KEY IDENTITY(1,1),
    TourId     INT NOT NULL,
    PoiId      INT NOT NULL,
    OrderIndex INT NOT NULL DEFAULT 0,  -- thứ tự POI trong tour

    Status NVARCHAR(20)
        NOT NULL
        CHECK (Status IN ('PENDING', 'APPROVED', 'REJECTED', 'REMOVE_PENDING'))
        DEFAULT 'PENDING',
        -- PENDING        : owner xin vào tour, chờ admin duyệt
        -- APPROVED       : đã có trong tour
        -- REJECTED       : bị từ chối vào tour
        -- REMOVE_PENDING : owner xin rời tour, chờ admin duyệt xóa

    FOREIGN KEY (TourId) REFERENCES Tours(Id) ON DELETE CASCADE,
    FOREIGN KEY (PoiId)  REFERENCES POI(Id)   ON DELETE CASCADE,

    CONSTRAINT UQ_TourPOI UNIQUE (TourId, PoiId)
);
-- ========================
-- USER ACTIVITIES 
-- ========================
CREATE TABLE UserActivities (
    Id INT PRIMARY KEY IDENTITY(1,1),
    UserId INT NULL,
    Username NVARCHAR(255) NOT NULL,
    Role NVARCHAR(50) NOT NULL,
    ActivityType NVARCHAR(50) NOT NULL,
    Details NVARCHAR(MAX) NOT NULL,
    Timestamp DATETIME DEFAULT GETDATE()
);

-- ================================================
-- DỮ LIỆU MẪU
-- ================================================

-- ========================
-- LANGUAGES
-- ========================
INSERT INTO Languages (Code, Name, IsActive, OrderIndex)
VALUES
(N'vi', N'Tiếng Việt', 1, 1),
(N'en', N'English',    1, 2),
(N'ja', N'日本語',      1, 3),
(N'zh', N'中文',        1, 4);

-- ========================
-- USERS
-- ========================
INSERT INTO Users (Username, PasswordHash, Name, Email, Phone, Role)
VALUES
(N'admin',  N'$2a$11$Ck.hKHKgo40.rtZHd8ZkRuEJy6C0ozOI.DuV.I9VjpQLV6obn8bAq', N'Admin Tổng',                  N'admin@gmail.com',  N'0900000001', N'ADMIN'),
(N'owner1', N'$2a$11$Ck.hKHKgo40.rtZHd8ZkRuEJy6C0ozOI.DuV.I9VjpQLV6obn8bAq', N'Chủ Quán Ốc Vĩnh Khánh',     N'owner1@gmail.com', N'0901111111', N'OWNER'),
(N'owner2', N'$2a$11$Ck.hKHKgo40.rtZHd8ZkRuEJy6C0ozOI.DuV.I9VjpQLV6obn8bAq', N'Chủ Quán Ăn Vặt Vĩnh Khánh', N'owner2@gmail.com', N'0902222227', N'OWNER'),
(N'owner3', N'$2a$11$Ck.hKHKgo40.rtZHd8ZkRuEJy6C0ozOI.DuV.I9VjpQLV6obn8bAq', N'Chủ Quán Ốc Vĩnh Khánh',     N'owner3@gmail.com', N'0901111181', N'OWNER'),
(N'owner4', N'$2a$11$Ck.hKHKgo40.rtZHd8ZkRuEJy6C0ozOI.DuV.I9VjpQLV6obn8bAq', N'Chủ Quán Ăn Vặt Vĩnh Khánh', N'owner4@gmail.com', N'0902222922', N'OWNER'),
(N'owner5', N'$2a$11$Ck.hKHKgo40.rtZHd8ZkRuEJy6C0ozOI.DuV.I9VjpQLV6obn8bAq', N'Chủ Quán Ốc Vĩnh Khánh',     N'owner5@gmail.com', N'0901111311', N'OWNER'),
(N'owner6', N'$2a$11$Ck.hKHKgo40.rtZHd8ZkRuEJy6C0ozOI.DuV.I9VjpQLV6obn8bAq', N'Chủ Quán Ăn Vặt Vĩnh Khánh', N'owner6@gmail.com', N'0902422222', N'OWNER'),
(N'user1',  N'$2a$11$Ck.hKHKgo40.rtZHd8ZkRuEJy6C0ozOI.DuV.I9VjpQLV6obn8bAq', N'Nguyễn Văn A',                N'user1@gmail.com',  N'0900000004', N'CUSTOMER'),
(N'user2',  N'$2a$11$Ck.hKHKgo40.rtZHd8ZkRuEJy6C0ozOI.DuV.I9VjpQLV6obn8bAq', N'Trần Thị B',                  N'user2@gmail.com',  N'0900000005', N'CUSTOMER');

-- ========================
-- POI
-- ========================
INSERT INTO POI (Name, Description, Address, Phone, Latitude, Longitude, Radius, OwnerId)
VALUES
(N'Ốc Oanh Vĩnh Khánh',    N'Quán ốc nổi tiếng đông khách mỗi tối',    N'534 Vĩnh Khánh, Quận 4',  N'0901000001', 10.761009, 106.702436, 5, 6),
(N'Ốc Thảo Vĩnh Khánh',    N'Ốc tươi ngon, giá bình dân',              N'555 Vĩnh Khánh, Quận 4',  N'0901000002', 10.761103, 106.703445, 5, 4),
(N'Ốc Nho Vĩnh Khánh',     N'Quán ốc lâu đời, hương vị đậm đà',        N'307 Vĩnh Khánh, Quận 4',  N'0901000003', 10.760472, 106.703425, 5, 3),
(N'Hải sản 63 Vĩnh Khánh', N'Hải sản tươi sống, chế biến tại chỗ',     N'63 Vĩnh Khánh, Quận 4',   N'0901000004', 10.760408, 106.703725, 5, 2),
(N'Ốc Tô Vĩnh Khánh',      N'Ốc tô siêu to, ăn đã miệng',             N'200 Vĩnh Khánh, Quận 4',  N'0901000005', 10.761199, 106.704948, 5, 4),
(N'Ốc Đào Vĩnh Khánh',     N'Quán ốc nổi tiếng giới trẻ',              N'212B Vĩnh Khánh, Quận 4', N'0901000006', 10.760522, 106.707003, 5, 5),
(N'Ốc Xào Me 109',          N'Ốc xào me chua ngọt đặc trưng',           N'109 Vĩnh Khánh, Quận 4',  N'0901000007', 10.760588, 106.705034, 5, 3),
(N'Ốc 30K Vĩnh Khánh',     N'Ốc giá rẻ, đa dạng món',                 N'150 Vĩnh Khánh, Quận 4',  N'0901000008', 10.760872, 106.704454, 5, 3),
(N'Quán Nhậu Vĩnh Khánh',  N'Quán nhậu bình dân, đông vui',            N'320 Vĩnh Khánh, Quận 4',  N'0901000009', 10.761009, 106.705764, 5, 6),
(N'Ốc Cay Vĩnh Khánh',     N'Ốc cay đặc trưng, vị đậm đà',            N'400 Vĩnh Khánh, Quận 4',  N'0901000010', 10.760798, 106.706954, 5, 4);

-- ========================
-- AUDIO
-- LanguageId: 1=vi, 2=en, 3=ja, 4=zh
-- ========================
INSERT INTO Audio (PoiId, LanguageId, Script)
VALUES
-- POI 1 – Ốc Oanh Vĩnh Khánh
(1, 1, N'Bạn đang đứng trước quán Ốc Oanh, một trong những địa điểm nổi tiếng nhất tại khu ẩm thực Vĩnh Khánh. Không khí nơi đây luôn nhộn nhịp, đặc biệt vào buổi tối khi khách đến rất đông. Các món ốc được chế biến đa dạng như xào me, nướng mỡ hành hay rang muối, mang hương vị đậm đà khó quên. Đây là nơi lý tưởng để bạn trải nghiệm ẩm thực đường phố Sài Gòn cùng bạn bè.'),
(1, 2, N'You are standing in front of Oc Oanh, one of the most famous spots in Vinh Khanh food street. The atmosphere is always lively, especially in the evening when it gets crowded. The dishes include tamarind stir-fried snails, grilled snails with scallion oil, and salted roasted options. This is a perfect place to enjoy Saigon street food with friends.'),
(1, 3, N'ここはオック・オアンという有名な店です。特に夜になると多くの人で賑わいます。料理はタマリンド炒めやネギ油焼きなどがあり、味がとても豊かです。友達と一緒にサイゴンの屋台料理を楽しむのに最適な場所です。'),
(1, 4, N'这里是著名的Ốc Oanh餐厅，位于永庆美食街。晚上非常热闹，人流量很大。这里提供多种螺类美食，如酸角炒螺、葱油烤螺等。是体验西贡街头美食的理想地点。'),

-- POI 2 – Ốc Thảo Vĩnh Khánh
(2, 1, N'Quán Ốc Thảo là một địa điểm quen thuộc với những người yêu thích ẩm thực bình dân. Mặc dù không gian không quá lớn nhưng luôn sạch sẽ và thoải mái. Các món ăn được chế biến nhanh chóng từ nguyên liệu tươi sống, giữ được hương vị tự nhiên. Giá cả hợp lý khiến nơi đây trở thành lựa chọn phổ biến của sinh viên và người dân địa phương.'),
(2, 2, N'Oc Thao is a familiar place for those who love affordable street food. Although the space is not very large, it is clean and comfortable. Dishes are prepared quickly from fresh ingredients, preserving their natural flavors. The reasonable prices make it a popular choice among students and locals.'),
(2, 3, N'オック・タオは手頃な価格で人気のある店です。店内は広くありませんが清潔で快適です。新鮮な食材を使った料理は自然な味を保っています。学生や地元の人々に人気の場所です。'),
(2, 4, N'Ốc Thảo是一家深受欢迎的平价餐厅。虽然空间不大，但环境干净舒适。食物使用新鲜食材制作，保留原味。价格合理，深受学生和当地人喜爱。'),

-- POI 3 – Ốc Nho Vĩnh Khánh
(3, 1, N'Ốc Nho là một quán ốc lâu đời với hương vị truyền thống. Khi đến đây, bạn sẽ cảm nhận được phong cách ẩm thực quen thuộc của người Sài Gòn. Các món ăn được nêm nếm vừa miệng, không quá cầu kỳ nhưng luôn giữ được chất lượng ổn định. Đây là nơi phù hợp cho những ai muốn tìm lại hương vị xưa.'),
(3, 2, N'Oc Nho is a long-standing restaurant known for its traditional flavors. Here, you can experience authentic Saigon cuisine. The dishes are well-seasoned and simple but consistently delicious. It is a great place for those who want to rediscover classic tastes.'),
(3, 3, N'オック・ニョは伝統的な味で知られる老舗です。サイゴンの本格的な料理を楽しめます。味付けはシンプルですが安定した美味しさがあります。昔ながらの味を求める人におすすめです。'),
(3, 4, N'Ốc Nho是一家历史悠久的餐厅，以传统风味著称。在这里可以体验地道的西贡美食。菜品简单但味道稳定，非常适合怀旧的人。'),

-- POI 4 – Hải sản 63 Vĩnh Khánh
(4, 1, N'Hải sản 63 là địa điểm lý tưởng cho những ai yêu thích hải sản tươi sống. Bạn có thể trực tiếp chọn nguyên liệu và yêu cầu chế biến theo sở thích. Không gian rộng rãi, phù hợp cho nhóm bạn hoặc gia đình. Các món ăn luôn giữ được độ tươi ngon và hương vị tự nhiên.'),
(4, 2, N'Hai San 63 is perfect for seafood lovers. You can choose fresh ingredients and request your preferred cooking style. The spacious environment is great for groups and families. The dishes maintain freshness and natural flavors.'),
(4, 3, N'ハイサン63はシーフード好きに最適な場所です。新鮮な食材を選び、好みに応じて調理してもらえます。広い空間で家族や友人と楽しめます。'),
(4, 4, N'63海鲜是海鲜爱好者的理想选择。你可以挑选新鲜食材并选择烹饪方式。环境宽敞，适合家庭或朋友聚会。'),

-- POI 5 – Ốc Tô Vĩnh Khánh
(5, 1, N'Ốc Tô gây ấn tượng với cách phục vụ các món ăn trong tô lớn. Điều này giúp bạn dễ dàng chia sẻ với bạn bè. Các món ốc được chế biến đậm vị, đặc biệt là các món xào và nướng. Không khí quán luôn sôi động và vui vẻ.'),
(5, 2, N'Oc To stands out with its large bowl servings, making it easy to share. The dishes are rich in flavor, especially stir-fried and grilled options. The atmosphere is always lively and fun.'),
(5, 3, N'オック・トは大きなボウルで料理を提供するのが特徴です。シェアしやすく、味も濃厚です。店内はいつも活気があります。'),
(5, 4, N'Ốc Tô以大份量著称，适合分享。菜品味道浓郁，特别是炒和烤类。氛围热闹有趣。'),

-- POI 6 – Ốc Đào Vĩnh Khánh
(6, 1, N'Ốc Đào là điểm đến quen thuộc của giới trẻ Sài Gòn. Không gian rộng rãi, sạch sẽ và được thiết kế đẹp mắt. Các món ăn không chỉ ngon mà còn được trình bày hấp dẫn, phù hợp để chụp ảnh và check-in.'),
(6, 2, N'Oc Dao is a popular spot among young people in Saigon. The space is spacious, clean, and nicely designed. The dishes are not only delicious but also visually appealing, perfect for photos.'),
(6, 3, N'オック・ダオは若者に人気のスポットです。店内は広くて清潔で、デザインも美しいです。料理は見た目も良く、写真に最適です。'),
(6, 4, N'Ốc Đào是年轻人喜爱的打卡地点。环境宽敞干净，设计美观。食物不仅美味，还很适合拍照。'),

-- POI 7 – Ốc Xào Me 109
(7, 1, N'Quán ốc xào me nổi bật với hương vị chua ngọt đặc trưng. Nước sốt được pha chế hài hòa giữa các vị, tạo nên sự hấp dẫn riêng. Dù không gian nhỏ nhưng quán luôn đông khách nhờ chất lượng ổn định.'),
(7, 2, N'The tamarind snail shop is known for its unique sweet and sour flavor. The sauce is well-balanced, creating a distinctive taste. Despite its small space, it is always crowded due to consistent quality.'),
(7, 3, N'タマリンド味の店は独特の甘酸っぱい味で有名です。小さい店ですが、品質の高さで人気があります。'),
(7, 4, N'这家酸角炒螺店以独特的酸甜味著称。虽然空间不大，但因质量稳定而客人很多。'),

-- POI 8 – Ốc 30K Vĩnh Khánh
(8, 1, N'Ốc 30K là lựa chọn lý tưởng cho những ai muốn ăn ngon với chi phí thấp. Thực đơn đa dạng, giá cả hợp lý. Không gian đơn giản nhưng luôn đông vui và náo nhiệt.'),
(8, 2, N'Oc 30K is ideal for budget-friendly dining. The menu is diverse and affordable. The atmosphere is simple but always lively.'),
(8, 3, N'オック30Kは安くて美味しい店です。メニューが豊富で、雰囲気も活気があります。'),
(8, 4, N'Ốc 30K价格实惠，选择多样。环境简单但气氛热闹。'),

-- POI 9 – Quán Nhậu Vĩnh Khánh
(9, 1, N'Quán nhậu Vĩnh Khánh là nơi tụ tập quen thuộc vào buổi tối. Không khí sôi động với tiếng cười nói và âm nhạc. Đây là nơi lý tưởng để thư giãn sau một ngày làm việc.'),
(9, 2, N'This pub in Vinh Khanh is a popular evening gathering spot. The lively atmosphere is filled with laughter and music. It is a great place to relax after a long day.'),
(9, 3, N'この店は夜に人気の集まり場所です。音楽と笑い声で賑やかです。リラックスするのに最適です。'),
(9, 4, N'这家餐厅是晚上聚会的好地方，充满音乐和欢笑。适合放松。'),

-- POI 10 – Ốc Cay Vĩnh Khánh
(10, 1, N'Ốc cay nổi bật với vị cay nồng đặc trưng. Các món ăn được nêm nếm kỹ lưỡng, mang lại trải nghiệm mạnh mẽ cho người thưởng thức. Đây là địa điểm không thể bỏ qua nếu bạn yêu thích đồ ăn cay.'),
(10, 2, N'Spicy snail dishes here are known for their strong and bold flavors. The seasoning is carefully prepared to deliver a powerful taste experience. A must-visit for spicy food lovers.'),
(10, 3, N'スパイシーな巻貝料理が特徴です。味付けはしっかりしていて、刺激的な体験ができます。辛いものが好きな人におすすめです。'),
(10, 4, N'这里的辣味螺非常有特色，味道浓烈。适合喜欢吃辣的人。');

-- ========================
-- HISTORY
-- ========================
INSERT INTO History (PoiId, UserId, ListenDuration)
VALUES
(3, 8, 200),
(4, 9, 150),
(5, 8, 90),
(1, 9, 60),
(2, 8, 30);

-- ========================
-- POI IMAGES
-- ========================
INSERT INTO POIImages (PoiId, ImageUrl, IsThumbnail)
VALUES
(1, '/uploads/poi/poi_1_1776157591648.webp', 0),
(1, '/uploads/poi/poi_1_1776157591869.webp', 0),
(2, '/uploads/poi/poi_2_1776157619503.jpg', 1),
(10,'/uploads/poi/poi_10_1776157656609.jpg', 1),
(3, '/uploads/poi/poi_3_1776157775039.webp', 1),
(9, '/uploads/poi/poi_9_1776157798581.jpg', 1),
(4, '/uploads/poi/poi_4_1776158006046.jpeg', 0),
(4, '/uploads/poi/poi_4_1776158006158.webp', 0),
(5, '/uploads/poi/poi_5_1776158254400.jpg', 1);

-- ========================
-- TOURS
-- ========================
INSERT INTO Tours (Name, Description, CreatedBy)
VALUES
(N'Khám phá ẩm thực Vĩnh Khánh', N'Tour trải nghiệm các quán ốc nổi tiếng',          1),
(N'Top ốc bình dân',              N'Những quán ốc giá rẻ, chất lượng tốt cho sinh viên', 1),
(N'Ốc cay và đặc sản',           N'Dành cho những ai thích đồ ăn cay và đậm vị',       1);

-- ========================
-- TOUR - POI
-- ========================
INSERT INTO TourPOI (TourId, PoiId, OrderIndex, Status)
VALUES
-- Tour 1: Khám phá ẩm thực Vĩnh Khánh
(1, 1, 1, N'APPROVED'),  -- Ốc Oanh
(1, 3, 2, N'APPROVED'),  -- Ốc Nho
(1, 4, 3, N'APPROVED'),  -- Hải sản 63
(1, 6, 4, N'APPROVED'),  -- Ốc Đào

-- Tour 2: Top ốc bình dân
(2, 2, 1, N'APPROVED'),  -- Ốc Thảo
(2, 8, 2, N'PENDING'),   -- Ốc 30K
(2, 9, 3, N'REJECTED'),  -- Quán Nhậu Vĩnh Khánh

-- Tour 3: Ốc cay và đặc sản
(3, 7, 1, N'PENDING'),   -- Ốc Xào Me 109
(3, 10, 2, N'PENDING'),  -- Ốc Cay Vĩnh Khánh
(3, 5, 3, N'PENDING');   -- Ốc Tô Vĩnh Khánh

-- Cập nhật Tours sang PUBLISHED
UPDATE Tours SET Status = 'PUBLISHED' WHERE Status = 'DRAFT';
ALTER TABLE Users ADD IsActive BIT NOT NULL DEFAULT 1;
