USE TourGuideDB;

-- ========================
-- USERS
-- ========================
CREATE TABLE Users (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Username NVARCHAR(100),
    PasswordHash NVARCHAR(255),

    Name NVARCHAR(150),          -- Tên người dùng
    Email NVARCHAR(150),         -- Email
    Phone NVARCHAR(20),          -- SĐT

    Role NVARCHAR(50) CHECK (Role IN ('ADMIN', 'OWNER', 'CUSTOMER'))
);

-- ========================
-- POI
-- ========================
CREATE TABLE POI (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(255),
    Description NVARCHAR(MAX),
    Address NVARCHAR(255),

    Phone NVARCHAR(20),          -- SĐT địa điểm

    Latitude FLOAT,
    Longitude FLOAT,
    Radius INT,

    OwnerId INT,
    FOREIGN KEY (OwnerId) REFERENCES Users(Id)
);

-- ========================
-- AUDIO 
-- ========================
CREATE TABLE Audio (
    Id INT PRIMARY KEY IDENTITY(1,1),
    PoiId INT,
    Language NVARCHAR(10),
    Script NVARCHAR(MAX),
	AudioUrl NVARCHAR(255),
    FOREIGN KEY (PoiId) REFERENCES POI(Id)
);

-- ========================
-- HISTORY
-- ========================
CREATE TABLE History (
    Id INT PRIMARY KEY IDENTITY(1,1),
    PoiId INT,
    UserId INT,
    PlayTime DATETIME DEFAULT GETDATE(),

    FOREIGN KEY (PoiId) REFERENCES POI(Id),
    FOREIGN KEY (UserId) REFERENCES Users(Id)
);
-- ========================
-- POI IMAGES
-- ========================
CREATE TABLE POIImages (
    Id INT PRIMARY KEY IDENTITY(1,1),
    PoiId INT NOT NULL,                 -- liên kết POI
    ImageUrl NVARCHAR(500) NOT NULL,    -- đường dẫn ảnh
    IsThumbnail BIT DEFAULT 0,          -- ảnh đại diện

    CreatedAt DATETIME DEFAULT GETDATE(),

    FOREIGN KEY (PoiId) REFERENCES POI(Id)
);
-- ========================
-- TRIGGER
-- ========================
GO
CREATE TRIGGER trg_OnlyCustomerHistory
ON History
INSTEAD OF INSERT
AS
BEGIN
    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN Users u ON i.UserId = u.Id
        WHERE u.Role <> 'CUSTOMER'
    )
    BEGIN
        RAISERROR (N'Chỉ CUSTOMER mới được ghi lịch sử', 16, 1);
        RETURN;
    END

    INSERT INTO History (PoiId, UserId, PlayTime)
    SELECT PoiId, UserId, PlayTime
    FROM inserted;
END
GO
DROP TRIGGER trg_OnlyCustomerHistory;
-- ========================
-- DATA MẪU
-- ========================

INSERT INTO Users (Username, PasswordHash, Name, Email, Phone, Role)
VALUES
(N'admin', N'123456', N'Admin Tổng', N'admin@gmail.com', N'0900000001', N'ADMIN'),
(N'owner1', N'123456', N'Chủ Quán Ốc Vĩnh Khánh', N'owner1@gmail.com', N'0901111111', N'OWNER'),
(N'owner2', N'123456', N'Chủ Quán Ăn Vặt Vĩnh Khánh', N'owner2@gmail.com', N'0902222227', N'OWNER'),
(N'owner3', N'123456', N'Chủ Quán Ốc Vĩnh Khánh', N'owner3@gmail.com', N'0901111181', N'OWNER'),
(N'owner4', N'123456', N'Chủ Quán Ăn Vặt Vĩnh Khánh', N'owner4@gmail.com', N'0902222922', N'OWNER'),
(N'owner5', N'123456', N'Chủ Quán Ốc Vĩnh Khánh', N'owner5@gmail.com', N'0901111311', N'OWNER'),
(N'owner6', N'123456', N'Chủ Quán Ăn Vặt Vĩnh Khánh', N'owner6@gmail.com', N'0902422222', N'OWNER'),
(N'user1', N'123456', N'Nguyễn Văn A', N'user1@gmail.com', N'0900000004', N'CUSTOMER'),
(N'user2', N'123456', N'Trần Thị B', N'user2@gmail.com', N'0900000005', N'CUSTOMER');

INSERT INTO POI (Name, Description, Address, Phone, Latitude, Longitude, Radius, OwnerId)
VALUES
(N'Ốc Oanh Vĩnh Khánh', N'Quán ốc nổi tiếng đông khách mỗi tối', N'534 Vĩnh Khánh, Quận 4', N'0901000001', 10.761009, 106.702436, 10, 6),
(N'Ốc Thảo Vĩnh Khánh', N'Ốc tươi ngon, giá bình dân', N'555 Vĩnh Khánh, Quận 4', N'0901000002', 10.761103, 106.703445, 10, 4),
(N'Ốc Nho Vĩnh Khánh', N'Quán ốc lâu đời, hương vị đậm đà', N'307 Vĩnh Khánh, Quận 4', N'0901000003', 10.760472, 106.703425, 10, 3),
(N'Hải sản 63 Vĩnh Khánh', N'Hải sản tươi sống, chế biến tại chỗ', N'63 Vĩnh Khánh, Quận 4', N'0901000004', 10.760408, 106.703725, 10, 2),
(N'Ốc Tô Vĩnh Khánh', N'Ốc tô siêu to, ăn đã miệng', N'200 Vĩnh Khánh, Quận 4', N'0901000005', 10.761199, 106.704948, 10, 4),
(N'Ốc Đào Vĩnh Khánh', N'Quán ốc nổi tiếng giới trẻ', N'212B Vĩnh Khánh, Quận 4', N'0901000006', 10.760522, 106.707003, 10, 5),
(N'Ốc Xào Me 109', N'Ốc xào me chua ngọt đặc trưng', N'109 Vĩnh Khánh, Quận 4', N'0901000007', 10.760588, 106.705034, 10, 3),
(N'Ốc 30K Vĩnh Khánh', N'Ốc giá rẻ, đa dạng món', N'150 Vĩnh Khánh, Quận 4', N'0901000008', 10.760872, 106.704454, 10, 3),
(N'Quán Nhậu Vĩnh Khánh', N'Quán nhậu bình dân, đông vui', N'320 Vĩnh Khánh, Quận 4', N'0901000009', 10.761009, 106.705764, 10, 6),
(N'Ốc Cay Vĩnh Khánh', N'Ốc cay đặc trưng, vị đậm đà', N'400 Vĩnh Khánh, Quận 4', N'0901000010', 10.760798, 106.706954, 10, 4);

INSERT INTO Audio (PoiId, Language, AudioUrl, Script)
VALUES
(1, N'vi', N'audio/oc_oanh.mp3',
N'Bạn đang đến với quán Ốc Oanh Vĩnh Khánh, một trong những quán ốc nổi tiếng và đông khách nhất tại khu vực này. Khi đứng tại đây, bạn có thể cảm nhận rõ không khí nhộn nhịp đặc trưng của Sài Gòn về đêm, với tiếng trò chuyện rôm rả và mùi thơm hấp dẫn lan tỏa từ các món ăn. Quán nổi bật với nhiều món ốc được chế biến đậm đà như ốc xào me, ốc rang muối hay ốc nướng mỡ hành. Điểm đặc biệt chính là phần nước chấm được pha chế riêng, tạo nên hương vị khó quên. Đây là nơi rất phù hợp để bạn tụ tập cùng bạn bè và trải nghiệm ẩm thực đường phố.'),

(2, N'vi', N'audio/oc_thao.mp3',
N'Bạn đang đứng trước quán Ốc Thảo Vĩnh Khánh, một địa điểm quen thuộc với những người yêu thích ẩm thực bình dân. Quán nổi tiếng với nguyên liệu tươi ngon, được chế biến nhanh chóng ngay khi khách gọi món. Không gian đơn giản nhưng luôn sạch sẽ và thoải mái. Giá cả hợp lý giúp nơi đây trở thành lựa chọn yêu thích của nhiều bạn trẻ và sinh viên. Nếu bạn đang tìm một nơi vừa ngon vừa tiết kiệm, thì đây chắc chắn là một điểm dừng chân lý tưởng.'),

(3, N'vi', N'audio/oc_nho.mp3',
N'Ốc Nho là một trong những quán ốc lâu đời tại khu vực Vĩnh Khánh. Với nhiều năm hoạt động, quán đã xây dựng được danh tiếng nhờ hương vị đậm đà và ổn định. Các món ăn tại đây mang phong cách truyền thống, được nêm nếm vừa miệng và phù hợp với nhiều khẩu vị. Không gian quán mang lại cảm giác gần gũi, thân thiện, đúng chất một quán ăn địa phương. Đây là nơi bạn có thể trải nghiệm ẩm thực Sài Gòn một cách chân thật nhất.'),

(4, N'vi', N'audio/hai_san_63.mp3',
N'Hải sản 63 mang đến trải nghiệm thưởng thức hải sản tươi sống ngay tại chỗ. Tại đây, bạn có thể trực tiếp chọn nguyên liệu và yêu cầu chế biến theo sở thích của mình. Không gian quán rộng rãi, sạch sẽ, rất phù hợp cho các buổi tụ họp bạn bè hoặc gia đình. Các món ăn được chế biến nhanh chóng nhưng vẫn giữ được độ tươi và vị ngọt tự nhiên của hải sản. Đây là điểm đến lý tưởng cho những ai yêu thích các món hải sản chất lượng.'),

(5, N'vi', N'audio/oc_to.mp3',
N'Ốc Tô Vĩnh Khánh nổi bật với phong cách phục vụ độc đáo khi các món ăn được đựng trong những chiếc tô lớn, đầy đặn. Điều này giúp bạn có thể dễ dàng chia sẻ món ăn cùng bạn bè. Các món ốc tại đây được chế biến đậm vị, đặc biệt là các món xào và nướng. Không khí quán luôn sôi động, tạo cảm giác vui vẻ và thoải mái cho thực khách.'),

(6, N'vi', N'audio/oc_dao.mp3',
N'Ốc Đào là điểm đến quen thuộc của giới trẻ Sài Gòn khi nhắc đến khu ẩm thực Vĩnh Khánh. Quán có không gian rộng rãi, sạch sẽ và phong cách phục vụ chuyên nghiệp. Các món ăn được chế biến kỹ lưỡng, giữ được hương vị đặc trưng và được trình bày đẹp mắt. Đây không chỉ là nơi ăn uống mà còn là địa điểm check-in được nhiều bạn trẻ yêu thích.'),

(7, N'vi', N'audio/oc_xaome.mp3',
N'Món ốc xào me tại đây có hương vị chua ngọt đặc trưng, tạo ấn tượng ngay từ lần thử đầu tiên. Nước sốt me được pha chế hài hòa, kết hợp giữa vị chua, ngọt và một chút cay nhẹ. Quán tuy không lớn nhưng luôn đông khách nhờ chất lượng món ăn ổn định. Đây là địa điểm lý tưởng cho những ai yêu thích các món ăn đậm đà.'),

(8, N'vi', N'audio/oc_30k.mp3',
N'Ốc 30K mang đến nhiều lựa chọn món ăn với mức giá rất hợp lý. Đây là địa điểm quen thuộc của sinh viên và những người muốn thưởng thức ẩm thực ngon mà không tốn quá nhiều chi phí. Thực đơn đa dạng, từ các món ốc hấp, xào cho đến nướng. Không gian quán đơn giản nhưng luôn đông vui và nhộn nhịp.'),

(9, N'vi', N'audio/quan_nhau.mp3',
N'Quán nhậu Vĩnh Khánh là nơi tụ họp bạn bè lý tưởng sau những giờ làm việc. Không gian bình dân, gần gũi nhưng luôn tràn đầy năng lượng với tiếng nói cười rôm rả. Thực đơn đa dạng với nhiều món nhậu hấp dẫn, kết hợp cùng đồ uống mát lạnh. Đây là nơi giúp bạn thư giãn và tận hưởng không khí sôi động của Sài Gòn về đêm.'),

(10, N'vi', N'audio/oc_cay.mp3',
N'Ốc cay Vĩnh Khánh nổi bật với các món ăn mang vị cay nồng đặc trưng. Các món ốc tại đây được nêm nếm kỹ lưỡng, tạo nên hương vị đậm đà và hấp dẫn. Không gian quán thoáng mát, phục vụ nhanh chóng giúp bạn có trải nghiệm ăn uống thoải mái. Đây là điểm đến không thể bỏ qua nếu bạn là người yêu thích đồ ăn cay.');

-- TEST HISTORY
INSERT INTO History (PoiId, UserId)
VALUES
(1, 8),
(2, 9);

INSERT INTO POIImages (PoiId, ImageUrl, IsThumbnail)
VALUES
(1, N'images/oc_oanh_1.jpg', 1),
(1, N'images/oc_oanh_2.jpg', 0),
(2, N'images/oc_thao_1.jpg', 1),
(3, N'images/oc_nho_1.jpg', 1);