using AutoMapper;
using GameAuthAPI.DTOs;
using GameAuthAPI.Models;

namespace GameAuthAPI.Profiles
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Преобразование Item в ItemDto
            CreateMap<Item, ItemDto>();

            // Преобразование ItemDto в Item
            CreateMap<ItemDto, Item>();

            // Преобразование CreateItemDto в Item
            CreateMap<CreateItemDto, Item>();

            // Преобразование Player в PlayerDto
            CreateMap<Player, PlayerDto>();

            // Преобразование PlayerItem в PlayerItemDto
            CreateMap<PlayerItem, PlayerItemDto>()
                .ForMember(dest => dest.IsEquipped, opt => opt.MapFrom(src => src.IsEquipped));

            // Преобразование Quest в QuestDto
            CreateMap<Quest, QuestDto>();

            // Преобразование QuestDto в Quest
            CreateMap<QuestDto, Quest>();

            // Преобразование PlayerTrade в PlayerTradeDto
            CreateMap<PlayerTrade, PlayerTradeDto>();

            // Преобразование ChatMessage в ChatMessageDto
            CreateMap<ChatMessage, ChatMessageDto>()
                 .ForMember(dest => dest.SenderName, opt => opt.MapFrom(src => src.Sender.Name))
                 .ForMember(dest => dest.ReceiverName, opt => opt.MapFrom(src => src.Receiver != null ? src.Receiver.Name : null));

            CreateMap<ChatMessageDto, ChatMessage>();

            // Добавлено: Преобразование Achievement в AchievementDto
            CreateMap<Achievement, AchievementDto>();

            // Добавлено: Преобразование AchievementDto в Achievement
            CreateMap<AchievementDto, Achievement>();

            // Добавлено: Преобразование ItemStats в ItemStatsDto (если это необходимо)
            CreateMap<ItemStats, ItemStatsDto>();

            // Добавлено: Преобразование ItemStatsDto в ItemStats (если это необходимо)
            CreateMap<ItemStatsDto, ItemStats>();
        }
    }
}