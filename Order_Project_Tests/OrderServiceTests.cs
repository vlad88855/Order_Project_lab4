using Moq;
using Order_Project.Models;
using Order_Project.Services;
using Order_Project.Services.Intefraces;

namespace Order_Project_Tests
{
    public class OrderServiceTests
    {
        private readonly Mock<IInventoryService> _inventoryMock;
        private readonly Mock<IPaymentService> _paymentMock;
        private readonly Mock<INotificationService> _notificationMock;
        private readonly OrderService _service;

        public OrderServiceTests()
        {
            _inventoryMock = new Mock<IInventoryService>();
            _paymentMock = new Mock<IPaymentService>();
            _notificationMock = new Mock<INotificationService>();

            _service = new OrderService(_inventoryMock.Object, _paymentMock.Object, _notificationMock.Object);
        }
        //--------------------------------------------------------------------------------------------------------------
        /// <summary>
        /// Допоміжний метод для тестів Update та Remove
        /// </summary>
        private Order SetupSuccessfulOrder()
        {
            _inventoryMock.Setup(i => i.CheckStock("laptop", 1)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);
            var order = _service.CreateOrder("laptop", 1);
            _inventoryMock.Invocations.Clear();
            _paymentMock.Invocations.Clear();
            _notificationMock.Invocations.Clear();
            return order;
        }

        /// <summary>
        /// Перевіряє повний успішний цикл створення замовлення
        /// </summary>
        [Fact]
        public void CreateOrder_Success_ShouldProcessOrderCorrectly()
        {
            _inventoryMock.Setup(i => i.CheckStock("laptop", 1)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);

            var order = _service.CreateOrder("laptop", 1);

            Assert.NotNull(order);
            Assert.True(order.IsPaid);
            Assert.Contains(order, _service.GetOrders());

            _inventoryMock.Verify(i => i.CheckStock("laptop", 1), Times.Once);
            _inventoryMock.Verify(i => i.ReduceStock("laptop", 1), Times.Once);
            _paymentMock.Verify(p => p.ProcessPayment(It.IsAny<Order>()), Times.Once);
            _notificationMock.Verify(n => n.SendConfirmation(order), Times.Once);
            _inventoryMock.Verify(i => i.IncreaseStock(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        /// <summary>
        /// Перевіряє, що сервіс кидає виняток, якщо товару немає на складі
        /// </summary>
        [Fact]
        public void CreateOrder_NoStock_ShouldThrowInvalidOperationException()
        {
            _inventoryMock.Setup(i => i.CheckStock("laptop", 5)).Returns(false);

            Assert.Throws<InvalidOperationException>(() => _service.CreateOrder("laptop", 5));
        }

        /// <summary>
        /// Перевіряє логіку відкату якщо оплата не вдалася -
        /// замовлення не створюється, а товар повертається на склад
        /// </summary>
        [Fact]
        public void CreateOrder_PaymentFails_ShouldThrowAndRollbackStock()
        {
            _inventoryMock.Setup(i => i.CheckStock("laptop", 1)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(false);

            Assert.Throws<InvalidOperationException>(() => _service.CreateOrder("laptop", 1));

            _inventoryMock.Verify(i => i.ReduceStock("laptop", 1), Times.Once);
            _inventoryMock.Verify(i => i.IncreaseStock("laptop", 1), Times.Once);
            _notificationMock.Verify(n => n.SendConfirmation(It.IsAny<Order>()), Times.Never);
            Assert.Empty(_service.GetOrders());
        }

        /// <summary>
        /// Параметризований тест для перевірки валідації вхідних даних
        /// </summary>
        [Theory]
        [InlineData("", 1)]      // Порожній продукт
        [InlineData("laptop", 0)] // Нульова кількість
        [InlineData(null, 1)]    // Null продукт
        [InlineData("laptop", -2)]// Від'ємна кількість
        public void CreateOrder_InvalidInput_ShouldThrowArgumentException(string product, int quantity)
        {
            Assert.Throws<ArgumentException>(() => _service.CreateOrder(product, quantity));
        }

        /// <summary>
        /// Перевіряє успішне видалення замовлення та повернення товару на склад
        /// </summary>
        [Fact]
        public void RemoveOrder_Success_ShouldReturnTrueAndIncreaseStock()
        {
            var order = SetupSuccessfulOrder();

            bool result = _service.RemoveOrder(order.Id);

            Assert.True(result);
            Assert.Empty(_service.GetOrders());
            _inventoryMock.Verify(i => i.IncreaseStock(order.Product, order.Quantity), Times.Once);
        }

        /// <summary>
        /// Перевіряє, що видалення неіснуючого замовлення повертає false
        /// </summary>
        [Fact]
        public void RemoveOrder_NotFound_ShouldReturnFalse()
        {
            bool result = _service.RemoveOrder(int.MaxValue);

            Assert.False(result);
        }

        /// <summary>
        /// Перевіряє успішне оновлення кількості існуючого замовлення
        /// </summary>
        [Fact]
        public void UpdateOrder_Success_ShouldChangeQuantity()
        {
            var order = SetupSuccessfulOrder();

            bool result = _service.UpdateOrder(order.Id, 5);

            Assert.True(result);
            Assert.Equal(5, order.Quantity);
        }

        /// <summary>
        /// Перевіряє, що оновлення не відбувається при невалідній кількості.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void UpdateOrder_InvalidQuantity_ShouldReturnFalse(int invalidQuantity)
        {
            var order = SetupSuccessfulOrder(); // Початкова кількість = 1

            bool result = _service.UpdateOrder(order.Id, invalidQuantity);

            Assert.False(result);
            Assert.Equal(1, order.Quantity); // Кількість не змінилась
        }

        /// <summary>
        /// Перевірка, що оплата викликається з будь-яким об'єктом Order
        /// </summary>
        [Fact]
        public void CreateOrder_Verify_ItIsAny_Example()
        {
            _inventoryMock.Setup(i => i.CheckStock(It.IsAny<string>(), It.IsAny<int>())).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);

            _service.CreateOrder("mouse", 1);
            _service.CreateOrder("Headphones", 1);

            _paymentMock.Verify(p => p.ProcessPayment(It.IsAny<Order>()), Times.Exactly(2));
        }

        /// <summary>
        /// Перевіряє, що фінальне сповіщення надсилається для замовлення з id = 1
        /// </summary>
        [Fact]
        public void CreateOrder_Verify_ItIsPredicate_Example()
        {
            _inventoryMock.Setup(i => i.CheckStock(It.IsAny<string>(), It.IsAny<int>())).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);

            _service.CreateOrder("keyboard", 2);

            _notificationMock.Verify(n => n.SendConfirmation(
                It.Is<Order>(o => o.Id == 1 && o.Product == "keyboard")
            ), Times.Once);
        }
        /// <summary>
        /// Демонструє наявність замовлення у списку замовлень після додавання
        /// </summary>
        [Fact]
        public void GetOrders_ShouldReturnNotEmpty_AfterAddingOrder()
        {
            SetupSuccessfulOrder();

            var orders = _service.GetOrders();

            Assert.NotEmpty(orders);
        }
    }

}
