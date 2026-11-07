using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Utils.Converters;

namespace GT4.UI.Items;

public sealed class AttachmentItem : DataItemBase
{
  public AttachmentItem(AttachmentInfo attachment, DataConverterResolver dataConverterResolver,
    ICancellationTokenProvider cancellationTokenProvider, IAlertService alertService)
    : base(attachment.Data, dataConverterResolver(attachment.Data.Category), cancellationTokenProvider, alertService)
  {
    Attachment = attachment;
  }

  public AttachmentInfo Attachment { get; }
}
