-- BƯỚC 1: Tìm tên constraint thực tế của cột order_status
-- Chạy câu này trước để biết đúng tên
SELECT 
    cc.name AS ConstraintName,
    cc.definition AS ConstraintDefinition
FROM sys.check_constraints cc
JOIN sys.columns col ON cc.parent_object_id = col.object_id 
    AND cc.parent_column_id = col.column_id
JOIN sys.tables t ON cc.parent_object_id = t.object_id
WHERE t.name = 'Orders' 
  AND col.name = 'order_status';

-- --------------------------------------------------------
-- BƯỚC 2: Sau khi biết tên, chạy đoạn sau
-- (Thay CK__TEN_THUC_TE bằng tên tìm được ở bước 1)
-- --------------------------------------------------------

-- Lấy tên constraint tự động rồi DROP + ADD lại
DECLARE @ConstraintName NVARCHAR(200);

SELECT @ConstraintName = cc.name
FROM sys.check_constraints cc
JOIN sys.columns col ON cc.parent_object_id = col.object_id 
    AND cc.parent_column_id = col.column_id
JOIN sys.tables t ON cc.parent_object_id = t.object_id
WHERE t.name = 'Orders' 
  AND col.name = 'order_status';

PRINT 'Constraint name: ' + ISNULL(@ConstraintName, 'KHÔNG TÌM THẤY');

-- Nếu tìm thấy thì DROP
IF @ConstraintName IS NOT NULL
BEGIN
    EXEC('ALTER TABLE Orders DROP CONSTRAINT [' + @ConstraintName + ']');
    PRINT 'Đã xóa constraint cũ: ' + @ConstraintName;
END

-- Tạo lại constraint với đầy đủ giá trị bao gồm Hoàn thành
ALTER TABLE Orders ADD CONSTRAINT CK_Orders_OrderStatus
    CHECK (order_status IN (
        N'Đặt hàng',
        N'Xử lý',
        N'Vận chuyển',
        N'Hoàn thành',
        N'Hủy'
    ));

PRINT 'Tạo constraint mới thành công!';

-- Kiểm tra kết quả
SELECT name, definition 
FROM sys.check_constraints 
WHERE parent_object_id = OBJECT_ID('Orders');
