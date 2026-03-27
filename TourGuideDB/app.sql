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
(N'Ốc Đào Vĩnh Khánh', N'Quán ốc nổi tiếng giới trẻ', N'212B Vĩnh Khánh, Quận 4', N'0901000006', 10.760522, 106.707003, 10,5),
(N'Ốc Xào Me 109', N'Ốc xào me chua ngọt đặc trưng', N'109 Vĩnh Khánh, Quận 4', N'0901000007', 10.760588, 106.705034, 10, 3),
(N'Ốc 30K Vĩnh Khánh', N'Ốc giá rẻ, đa dạng món', N'150 Vĩnh Khánh, Quận 4', N'0901000008', 10.760872, 106.704454, 10, 3),
(N'Quán Nhậu Vĩnh Khánh', N'Quán nhậu bình dân, đông vui', N'320 Vĩnh Khánh, Quận 4', N'0901000009', 10.761009, 106.705764, 10, 6),
(N'Ốc Cay Vĩnh Khánh', N'Ốc cay đặc trưng, vị đậm đà', N'400 Vĩnh Khánh, Quận 4', N'0901000010', 10.760798, 106.706954, 10, 4);

INSERT INTO Audio (PoiId, Language, AudioUrl, Script)
VALUES
(1, N'vi', N'audio/oc_oanh.mp3', N'Bạn đang đến quán Ốc Oanh Vĩnh Khánh, một trong những quán ốc nổi tiếng nhất khu vực...'),
(2, N'vi', N'audio/oc_thao.mp3', N'Ốc Thảo Vĩnh Khánh nổi tiếng với hải sản tươi ngon và giá cả hợp lý...'),
(3, N'vi', N'audio/oc_nho.mp3', N'Ốc Nho là quán ốc lâu đời với hương vị đặc trưng...'),
(4, N'vi', N'audio/hai_san_63.mp3', N'Hải sản 63 mang đến trải nghiệm hải sản tươi sống ngay tại bàn...'),
(5, N'vi', N'audio/oc_to.mp3', N'Ốc Tô Vĩnh Khánh nổi bật với phần ăn lớn và hấp dẫn...'),
(6, N'vi', N'audio/oc_dao.mp3', N'Ốc Đào là điểm đến quen thuộc của giới trẻ Sài Gòn...'),
(7, N'vi', N'audio/oc_xaome.mp3', N'Món ốc xào me tại đây có vị chua ngọt đặc trưng...'),
(8, N'vi', N'audio/oc_30k.mp3', N'Ốc 30K mang đến nhiều lựa chọn với giá cực kỳ hợp lý...'),
(9, N'vi', N'audio/quan_nhau.mp3', N'Quán nhậu Vĩnh Khánh là nơi tụ họp bạn bè lý tưởng...'),
(10, N'vi', N'audio/oc_cay.mp3', N'Ốc cay Vĩnh Khánh nổi bật với vị cay nồng hấp dẫn...');

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