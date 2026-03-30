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
(N'Ốc Oanh Vĩnh Khánh', 
 N'Ốc Oanh là một trong những quán ốc nổi tiếng nhất tại khu Vĩnh Khánh, đặc biệt thu hút đông đảo thực khách vào mỗi buổi tối. Không gian quán tuy không quá rộng nhưng luôn nhộn nhịp, tạo cảm giác rất "Sài Gòn về đêm". Các món ốc tại đây được chế biến đậm đà với nhiều hương vị như xào me, rang muối, nướng mỡ hành. Đặc biệt, nước chấm tại quán được pha chế theo công thức riêng, làm tăng thêm độ hấp dẫn cho từng món ăn. Đây là địa điểm lý tưởng để tụ tập bạn bè, thưởng thức ẩm thực đường phố và tận hưởng không khí sôi động.', 
 N'534 Vĩnh Khánh, Quận 4', N'0901000001', 10.761009, 106.702436, 10, 6),

(N'Ốc Thảo Vĩnh Khánh', 
 N'Ốc Thảo mang đến trải nghiệm ẩm thực bình dân nhưng chất lượng đáng ngạc nhiên. Quán nổi bật với nguồn nguyên liệu tươi ngon, được nhập mỗi ngày và chế biến ngay tại chỗ. Không gian thoáng mát, phù hợp cho cả nhóm bạn lẫn gia đình. Các món ăn tại đây không chỉ đa dạng mà còn có giá cả rất hợp lý, khiến quán luôn đông khách vào giờ cao điểm. Nếu bạn đang tìm một nơi vừa ngon, vừa rẻ để thưởng thức ốc thì đây chắc chắn là lựa chọn không thể bỏ qua.', 
 N'555 Vĩnh Khánh, Quận 4', N'0901000002', 10.761103, 106.703445, 10, 4),

(N'Ốc Nho Vĩnh Khánh', 
 N'Ốc Nho là quán ốc lâu đời tại khu vực Vĩnh Khánh, nổi tiếng với hương vị đậm đà và cách chế biến truyền thống. Trải qua nhiều năm hoạt động, quán vẫn giữ được chất lượng ổn định và phong cách phục vụ thân thiện. Các món ăn được nêm nếm vừa miệng, phù hợp với khẩu vị của nhiều người. Đây là địa điểm quen thuộc của người dân địa phương cũng như du khách muốn trải nghiệm ẩm thực ốc mang đậm chất Sài Gòn xưa.', 
 N'307 Vĩnh Khánh, Quận 4', N'0901000003', 10.760472, 106.703425, 10, 3),

(N'Hải sản 63 Vĩnh Khánh', 
 N'Hải sản 63 là nơi lý tưởng dành cho những ai yêu thích hải sản tươi sống. Tại đây, thực khách có thể tự chọn nguyên liệu và yêu cầu chế biến theo sở thích. Không gian quán rộng rãi, sạch sẽ, phù hợp cho các buổi tụ họp đông người. Các món ăn được chế biến nhanh chóng, giữ được độ tươi và vị ngọt tự nhiên của hải sản. Đây là địa điểm đáng thử nếu bạn muốn trải nghiệm hải sản chất lượng ngay giữa lòng thành phố.', 
 N'63 Vĩnh Khánh, Quận 4', N'0901000004', 10.760408, 106.703725, 10, 2),

(N'Ốc Tô Vĩnh Khánh', 
 N'Ốc Tô gây ấn tượng với phong cách phục vụ độc đáo khi các món ăn được đựng trong tô lớn, đầy đặn và bắt mắt. Quán phù hợp với những ai thích ăn no và chia sẻ món ăn cùng bạn bè. Các món ốc tại đây được chế biến đậm vị, đặc biệt là các món xào và nướng. Không khí quán luôn sôi động, tạo cảm giác vui vẻ và thoải mái cho thực khách.', 
 N'200 Vĩnh Khánh, Quận 4', N'0901000005', 10.761199, 106.704948, 10, 4),

(N'Ốc Đào Vĩnh Khánh', 
 N'Ốc Đào là cái tên quen thuộc đối với giới trẻ Sài Gòn. Quán nổi bật với không gian rộng rãi, sạch sẽ và phong cách phục vụ chuyên nghiệp. Các món ăn tại đây được chế biến tinh tế, giữ được hương vị đặc trưng và trình bày đẹp mắt. Đây không chỉ là nơi ăn uống mà còn là điểm check-in quen thuộc của nhiều bạn trẻ.', 
 N'212B Vĩnh Khánh, Quận 4', N'0901000006', 10.760522, 106.707003, 10, 5),

(N'Ốc Xào Me 109', 
 N'Ốc Xào Me 109 nổi tiếng với món ốc xào me có hương vị chua ngọt đặc trưng, hấp dẫn ngay từ lần thử đầu tiên. Quán tuy nhỏ nhưng luôn đông khách nhờ chất lượng món ăn ổn định. Nước sốt me được pha chế khéo léo, tạo nên sự cân bằng hoàn hảo giữa vị chua, ngọt và cay nhẹ.', 
 N'109 Vĩnh Khánh, Quận 4', N'0901000007', 10.760588, 106.705034, 10, 3),

(N'Ốc 30K Vĩnh Khánh', 
 N'Ốc 30K là lựa chọn lý tưởng cho sinh viên và những ai muốn ăn ngon với chi phí thấp. Mỗi món tại đây có giá rất dễ tiếp cận nhưng vẫn đảm bảo chất lượng. Thực đơn đa dạng, từ ốc hấp, ốc xào đến các món nướng, phù hợp với nhiều khẩu vị khác nhau.', 
 N'150 Vĩnh Khánh, Quận 4', N'0901000008', 10.760872, 106.704454, 10, 3),

(N'Quán Nhậu Vĩnh Khánh', 
 N'Quán Nhậu Vĩnh Khánh mang đến không gian bình dân, gần gũi nhưng luôn nhộn nhịp và vui vẻ. Đây là nơi lý tưởng để tụ tập bạn bè sau giờ làm, thưởng thức các món nhậu đa dạng cùng đồ uống mát lạnh. Không khí quán luôn sôi động, đặc biệt vào buổi tối.', 
 N'320 Vĩnh Khánh, Quận 4', N'0901000009', 10.761009, 106.705764, 10, 6),

(N'Ốc Cay Vĩnh Khánh', 
 N'Ốc Cay nổi bật với các món ăn có vị cay đặc trưng, phù hợp với những ai yêu thích ẩm thực đậm đà. Các món ốc tại đây được nêm nếm kỹ lưỡng, tạo nên hương vị riêng biệt khó quên. Quán có không gian thoáng mát, phục vụ nhanh chóng, là điểm đến quen thuộc của nhiều tín đồ ăn cay.', 
 N'400 Vĩnh Khánh, Quận 4', N'0901000010', 10.760798, 106.706954, 10, 4);

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