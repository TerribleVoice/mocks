using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using FakeItEasy;
using FileSender.Dependencies;
using FluentAssertions;
using NUnit.Framework;

namespace FileSender
{
    public class FileSender
    {
        private readonly ICryptographer cryptographer;
        private readonly ISender sender;
        private readonly IRecognizer recognizer;

        public FileSender(
            ICryptographer cryptographer,
            ISender sender,
            IRecognizer recognizer)
        {
            this.cryptographer = cryptographer;
            this.sender = sender;
            this.recognizer = recognizer;
        }

        public Result SendFiles(File[] files, X509Certificate certificate)
        {
            return new Result
            {
                SkippedFiles = files
                    .Where(file => !TrySendFile(file, certificate))
                    .ToArray()
            };
        }

        private bool TrySendFile(File file, X509Certificate certificate)
        {
            Document document;
            if (!recognizer.TryRecognize(file, out document))
                return false;
            if (!CheckFormat(document) || !CheckActual(document))
                return false;
            var signedContent = cryptographer.Sign(document.Content, certificate);
            return sender.TrySend(signedContent);
        }

        private bool CheckFormat(Document document)
        {
            return document.Format == "4.0" ||
                   document.Format == "3.1";
        }

        private bool CheckActual(Document document)
        {
            return document.Created.AddMonths(1) > DateTime.Now;
        }

        public class Result
        {
            public File[] SkippedFiles { get; set; }
        }
    }

    //TODO: реализовать недостающие тесты
    [TestFixture]
    public class FileSender_Should
    {
        private FileSender fileSender;
        private ICryptographer cryptographer;
        private ISender sender;
        private IRecognizer recognizer;

        private readonly X509Certificate certificate = new X509Certificate();
        private File file;
        private byte[] signedContent;

        [SetUp]
        public void SetUp()
        {
            // Постарайтесь вынести в SetUp всё неспецифическое конфигурирование так,
            // чтобы в конкретных тестах осталась только специфика теста,
            // без конфигурирования "обычного" сценария работы

            file = new File("someFile", new byte[] {1, 2, 3});
            signedContent = new byte[] {1, 7};

            cryptographer = A.Fake<ICryptographer>();
            sender = A.Fake<ISender>();
            recognizer = A.Fake<IRecognizer>();
            fileSender = new FileSender(cryptographer, sender, recognizer);
            var goodDocument = new Document(file.Name, file.Content, DateTime.Now, "4.0");
            A.CallTo(() => recognizer.TryRecognize(A<File>.Ignored, out goodDocument))
                .Returns(true);
            A.CallTo(() => cryptographer.Sign(A<byte[]>.Ignored, A<X509Certificate>.Ignored))
                .Returns(signedContent);
            A.CallTo(() => sender.TrySend(A<byte[]>.Ignored))
                .Returns(true);
        }

        [TestCase("4.0")]
        [TestCase("3.1")]
        public void Send_WhenGoodFormat(string format)
        {
            var document = new Document(file.Name, file.Content, DateTime.Now, format);
            A.CallTo(() => recognizer.TryRecognize(A<File>.Ignored, out document))
                .Returns(true);

            fileSender.SendFiles(new[] {file}, certificate)
                .SkippedFiles.Should().BeEmpty();
        }

        [Test]
        public void Skip_WhenBadFormat()
        {
            var document = new Document(file.Name, file.Content, DateTime.Now, "bad format");
            A.CallTo(() => recognizer.TryRecognize(A<File>.Ignored, out document))
                .Returns(true);
            
            fileSender.SendFiles(new[] {file}, certificate)
                .SkippedFiles.Should().Contain(file);
        }

        [Test]
        public void Skip_WhenOlderThanAMonth()
        {
            var badDateTime = DateTime.Now.AddMonths(-2);
            var document = new Document(file.Name, file.Content, badDateTime, "4.0");
            A.CallTo(() => recognizer.TryRecognize(A<File>.Ignored, out document))
                .Returns(true);
            
            fileSender.SendFiles(new[] {file}, certificate)
                .SkippedFiles.Should().Contain(file);
        }

        [Test]
        public void Send_WhenYoungerThanAMonth()
        {
            var goodDateTime = DateTime.Now.AddDays(-25);
            var document = new Document(file.Name, file.Content, goodDateTime, "4.0");
            A.CallTo(() => recognizer.TryRecognize(A<File>.Ignored, out document))
                .Returns(true);
            
            fileSender.SendFiles(new[] {file}, certificate)
                .SkippedFiles.Should().BeEmpty();
        }

        [Test]
        public void Skip_WhenSendFails()
        {
            A.CallTo(() => sender.TrySend(A<byte[]>.Ignored)).Returns(false);
            
            fileSender.SendFiles(new[] {file}, certificate)
                .SkippedFiles.Should().Contain(file);
        }

        [Test]
        public void Skip_WhenNotRecognized()
        {
            var document = new Document(file.Name, file.Content, DateTime.Now, "4.0");
            A.CallTo(() => recognizer.TryRecognize(A<File>.Ignored, out document))
                .Returns(false);
            
            fileSender.SendFiles(new[] {file}, certificate)
                .SkippedFiles.Should().Contain(file);
        }

        [Test]
        public void IndependentlySend_WhenSeveralFilesAndSomeAreInvalid()
        {
            var badFile = new File("badName", new byte[0]);
            var badDocument = new Document(badFile.Name, badFile.Content, DateTime.Now, "4.0");
            A.CallTo(() => recognizer.TryRecognize(badFile, out badDocument))
                .Returns(false);
            
            fileSender.SendFiles(new[] {file, badFile}, certificate)
                .SkippedFiles.Should().BeEquivalentTo(badFile);
        }

        [Test]
        public void IndependentlySend_WhenSeveralFilesAndSomeCouldNotSend()
        {
            var badFile = new File("badName", new byte[] {1});
            var badDocument = new Document(badFile.Name, badFile.Content, DateTime.Now, "4.0");
            A.CallTo(() => recognizer.TryRecognize(badFile, out badDocument))
                .Returns(true);
            A.CallTo(() => cryptographer.Sign(badFile.Content, certificate))
                .Returns(badFile.Content);
            A.CallTo(() => sender.TrySend(badFile.Content))
                .Returns(false);
            
            fileSender.SendFiles(new[] {file, badFile}, certificate)
                .SkippedFiles.Should().BeEquivalentTo(badFile);
        }
    }
}
