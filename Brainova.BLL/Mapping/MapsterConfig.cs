using Brainova.BLL.DTOs.Response;

using Brainova.DAL.Modles;
using Mapster;

namespace Brainova.BLL.Mapping
{
    public class MapsterConfig : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<MriCase, MriUploadResponse>()
      .Map(dest => dest.CaseId, src => src.Id)
      .Map(dest => dest.StoredFileName, src => src.StoredFileName);
        }
    }
}