using ProjectM;
using Stunlock.Core;
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireCommandFramework;


namespace NameOfYourMod;

[CommandGroup("Test", "t")]
public  class ResourceCommands
{
    private static World GetServerWorld(ChatCommandContext ctx)
    {
        foreach (var w in World.All)
        {
            if (w != null && w.IsCreated && w.Name == "Server")
                return w;
        }
        ctx.Reply("Server 世界未初始化。");
        return null;
    }
    [Command("tips",shortHand:"t",adminOnly: false,usage:".t tips", description: "使用.t tips获取提示")]
    public static void TipsCommand(ChatCommandContext ctx)
    {
        ctx.Reply("欢迎使用测试命令！\n" +
                  "其余命令请输入adminauth \n"+
                  "可用命令：\n" +
                  ".t f5 - 刷新资源\n" +
                  ".t a <PrefabId> [数量] [半径] - 在鼠标位置生成实体可选圆圈分布\n" +
                  ".t d  - 删除鼠标悬停的实体\n" +
                  ".t chp <具体值|百分比> - 修改鼠标附近最近实体的生命值\n" )
            ;
    }

    [Command("refresh", shortHand: "f5", adminOnly: true, usage: ".t f5", description: "刷新你指向的资源")]
    public static void RefreshTargetResource(ChatCommandContext ctx)
    {
        var world = GetServerWorld(ctx);
        if (world == null) return;

        var em = world.EntityManager;
        var character = ctx.Event.SenderCharacterEntity;

        if (!em.Exists(character) || !em.HasComponent<EntityInput>(character))
        {
            ctx.Reply("玩家实体无效。");
            return;
        }

        float3 aimPos = em.GetComponentData<EntityInput>(character).AimPosition;
        Entity target = Entity.Null;
        float minDist = float.MaxValue;

        // 找到最近的资源实体
        foreach (var entity in em.GetAllEntities())
        {
            if (em.HasComponent<PrefabGUID>(entity) && em.HasComponent<AutoChainInstanceData>(entity))
            {
                float3 pos = em.HasComponent<Translation>(entity)
                    ? em.GetComponentData<Translation>(entity).Value
                    : em.GetComponentData<LocalTransform>(entity).Position;

                float dist = math.distance(pos, aimPos);
                if (dist < minDist)
                {
                    minDist = dist;
                    target = entity;
                }
            }
        }

        if (target != Entity.Null)
        {
            var ac = em.GetComponentData<AutoChainInstanceData>(target);
            ac.NextTransitionAttempt = world.Time.ElapsedTime;
            em.SetComponentData(target, ac);
            ctx.Reply("已立即刷新目标资源。");
        }
        else
        {
            ctx.Reply("没有找到可刷新的资源。");
        }
    }
   
    [Command("d", adminOnly: true, usage: ".t d", description: "删除鼠标下的实体")]
    public static void DeleteEntity(ChatCommandContext ctx)
    {
        var world = GetServerWorld(ctx);
        if (world == null)
        {
            ctx.Reply("无法获取 World。");
            return;
        }

        var em = world.EntityManager;
        var character = ctx.Event.SenderCharacterEntity;

        if (!em.Exists(character) || !em.HasComponent<EntityInput>(character))
        {
            ctx.Reply("无法获取玩家的 EntityInput。");
            return;
        }

        var input = em.GetComponentData<EntityInput>(character);
        Entity target = input.HoveredEntity;

        if (target == Entity.Null || !em.Exists(target))
        {
            ctx.Reply("鼠标下没有实体。");
            return;
        }

        // 安全检查，避免误删玩家自己
        if (target == character)
        {
            ctx.Reply("不能删除玩家自己。");
            return;
        }

        // 删除
        em.DestroyEntity(target);
        ctx.Reply($"已删除实体 {target.Index}");
        Plugin.LogInstance.LogInfo($"[DeleteEntity] Deleted entity {target.Index}");
    }


    [Command("changehp", shortHand: "chp", adminOnly: true, usage: ".t chp <具体值|百分比>", description: "修改鼠标附近最近实体的当前生命值")]
    public static void ChangeHealthOfClosestToMouseCommand(ChatCommandContext ctx, string value)
    {
        try
        {
            // 找服务端世界
            World serverWorld = null;
            foreach (var world in World.All)
            {
                if (world.Name == "Server")
                {
                    serverWorld = world;
                    break;
                }
            }
            if (serverWorld == null)
            {
                ctx.Reply("未找到服务端世界。");
                return;
            }
            var em = serverWorld.EntityManager;

            // 获取玩家输入
            Entity character = ctx.Event.SenderCharacterEntity;
            if (!em.Exists(character) || !em.HasComponent<EntityInput>(character))
            {
                ctx.Reply("玩家实体无效或缺少 EntityInput 组件。");
                return;
            }
            var entityInput = em.GetComponentData<EntityInput>(character);
            Entity hoveredEntity = entityInput.HoveredEntity;

            if (!em.Exists(hoveredEntity))
            {
                ctx.Reply("鼠标下没有找到实体。");
                return;
            }

            // 检查 Health
            if (!em.HasComponent<Health>(hoveredEntity))
            {
                ctx.Reply("目标实体没有 Health 组件。");
                return;
            }

            var health = em.GetComponentData<Health>(hoveredEntity);
            float originalCurrent = health.Value;
            float newValue;

            // 百分比 or 绝对值
            if (value.EndsWith("%"))
            {
                if (float.TryParse(value.TrimEnd('%'), out float percent))
                    newValue = originalCurrent * (percent / 100f);
                else
                {
                    ctx.Reply("无效的百分比输入。");
                    return;
                }
            }
            else
            {
                if (!float.TryParse(value, out newValue))
                {
                    ctx.Reply("无效的生命值输入。");
                    return;
                }
            }

            // 改当前 HP
            health.Value = newValue;
            em.SetComponentData(hoveredEntity, health);

            ctx.Reply($"已将 {hoveredEntity} 当前生命值改为 {newValue}");
            Plugin.LogInstance.LogInfo($"[ChangeHP] Changed current health of {hoveredEntity} to {newValue}");
        }
        catch (Exception ex)
        {
            ctx.Reply("修改生命值时发生错误，请查看服务器日志。");
            Plugin.LogInstance.LogError($"[ChangeHP] Exception: {ex}");
        }
    }
    public static class SpawnRefreshCommand
    {
        [Command("spawnrefresh", shortHand: "f5", adminOnly: true, usage: ".t f5 <Id> [count] [delay]",
            description: "生成实体并自动按延迟时间刷新（秒）")]
        public static void SpawnAndAutoRefresh(ChatCommandContext ctx, int prefabId, int count = 1, float refreshDelay = 30f)
        {
            try
            {
                World world = null;
                foreach (var w in World.All)
                {
                    if (w.Name == "Server")
                    {
                        world = w;
                        break;
                    }
                }
                if (world == null)
                {
                    ctx.Reply("未找到服务端世界。");
                    return;
                }


                var em = world.EntityManager;
                var character = ctx.Event.SenderCharacterEntity;

                if (!em.Exists(character) || !em.HasComponent<EntityInput>(character))
                {
                    ctx.Reply("玩家实体无效。");
                    return;
                }

                var entityInput = em.GetComponentData<EntityInput>(character);
                float3 pos = entityInput.AimPosition;

                var prefabGuid = new PrefabGUID(prefabId);
                if (!GameWorldUtils.Server.GetExistingSystemManaged<PrefabCollectionSystem>()._PrefabGuidToEntityMap.TryGetValue(prefabGuid, out var prefabEntity))
                {
                    ctx.Reply($"无效的 PrefabID: {prefabId}");
                    return;
                }

                for (int i = 0; i < count; i++)
                {
                    Entity spawned = em.Instantiate(prefabEntity);
                    float3 offset = new float3(i * 1.5f, 0, 0);

                    if (em.HasComponent<LocalTransform>(spawned))
                    {
                        var lt = em.GetComponentData<LocalTransform>(spawned);
                        lt.Position = pos + offset;
                        em.SetComponentData(spawned, lt);
                    }
                    else if (em.HasComponent<Translation>(spawned))
                    {
                        em.SetComponentData(spawned, new Translation { Value = pos + offset });
                    }

                    // 添加并设置刷新数据
                    em.AddComponent<SpawnRefreshData>(spawned);
                    em.SetComponentData(spawned, new SpawnRefreshData
                    {
                        Prefab = prefabGuid,
                        RefreshDelay = refreshDelay,
                        NextRefreshTime = world.Time.ElapsedTime + refreshDelay,
                        SpawnPosition = pos + offset
                    });
                }

                ctx.Reply($"已生成 {count} 个实体: {prefabGuid} [ID:{prefabId}]，刷新延迟: {refreshDelay}秒");
            }
            catch (Exception ex)
            {
                ctx.Reply("生成刷新实体失败，请查看服务器日志。");
                Plugin.LogInstance.LogError($"[SpawnRefresh] Exception: {ex}");
            }
        }
    }

    public struct SpawnRefreshData
    {
        public PrefabGUID Prefab;
        public float RefreshDelay;
        public double NextRefreshTime;
        public float3 SpawnPosition;
    }

    public partial class SpawnRefreshSystem : SystemBase
    {
        public override void OnUpdate()
        {
            var worldTime = World.Time.ElapsedTime;
            var em = EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var refreshQuery = GetEntityQuery(ComponentType.ReadWrite<SpawnRefreshData>());
            var entities = refreshQuery.ToEntityArray(Allocator.Temp);
            var refreshDataArray = refreshQuery.ToComponentDataArray<SpawnRefreshData>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var data = refreshDataArray[i];
                bool shouldRefresh = false;

                if (!em.Exists(entity) || em.HasComponent<DestroyTag>(entity))
                {
                    shouldRefresh = true;
                }
                else if (worldTime >= data.NextRefreshTime)
                {
                    shouldRefresh = true;
                }

                if (shouldRefresh)
                {
                    if (em.Exists(entity))
                        ecb.DestroyEntity(entity);

                    if (Core.PrefabCollectionSystem._PrefabGuidToEntityMap.TryGetValue(data.Prefab, out var prefabEntity) && prefabEntity != Entity.Null)
                    {
                        var newEntity = ecb.Instantiate(prefabEntity);

                        if (em.HasComponent<LocalTransform>(newEntity))
                        {
                            var lt = em.GetComponentData<LocalTransform>(newEntity);
                            lt.Position = data.SpawnPosition;
                            ecb.SetComponent(newEntity, lt);
                        }
                        else if (em.HasComponent<Translation>(newEntity))
                        {
                            ecb.SetComponent(newEntity, new Translation { Value = data.SpawnPosition });
                        }

                        var newData = new SpawnRefreshData
                        {
                            Prefab = data.Prefab,
                            RefreshDelay = data.RefreshDelay,
                            NextRefreshTime = worldTime + data.RefreshDelay,
                            SpawnPosition = data.SpawnPosition
                        };
                        ecb.SetComponent(newEntity, newData);
                    }
                }
            }

            entities.Dispose();
            refreshDataArray.Dispose();
            ecb.Playback(em);
            ecb.Dispose();
        }
    }

    [Command("debugself",shortHand: "debug1", adminOnly: true, usage: ".t debugself", description: "打印玩家实体的所有组件")]
    public static void DebugSelf(ChatCommandContext ctx)
    {
        var world = GetServerWorld(ctx);
        if (world == null)
        {
            ctx.Reply("无法获取 World。");
            return;
        }

        var em = world.EntityManager;
        var character = ctx.Event.SenderCharacterEntity;

        if (!em.Exists(character))
        {
            ctx.Reply("玩家实体无效。");
            return;
        }

        var types = em.GetComponentTypes(character, Allocator.Temp);
        try
        {
            ctx.Reply($"玩家实体 {character.Index} 组件数量: {types.Length}");
            foreach (var comp in types)
            {
                string compName = comp.ToString();
                ctx.Reply($" - {compName}");
                Plugin.LogInstance.LogInfo($"[DebugSelf] {compName}");
            }
        }
        finally
        {
            types.Dispose();
        }
    }

    [Command("prefabinfo",shortHand:"info", adminOnly: true, usage: ".t prefabinfo <PrefabGUID>", description: "打印指定 Prefab 实体的所有组件")]
    public static void PrefabInfo(ChatCommandContext ctx, int prefabId)
    {
        var world = GetServerWorld(ctx);
        if (world == null)
        {
            ctx.Reply("无法获取 World。");
            return;
        }

        var em = world.EntityManager;
        var prefabCollection = world.GetExistingSystemManaged<PrefabCollectionSystem>();

        if (prefabCollection == null)
        {
            ctx.Reply("PrefabCollectionSystem 不存在。");
            return;
        }

        // PrefabGUID 是个 struct，直接用 prefabId 构造
        var guid = new PrefabGUID(prefabId);
        if (!prefabCollection._PrefabGuidToEntityMap.TryGetValue(guid, out Entity prefabEntity)
            || prefabEntity == Entity.Null
            || !em.Exists(prefabEntity))
        {
            ctx.Reply($"找不到 PrefabGUID={prefabId} 的实体。");
            return;
        }

        ctx.Reply($"Prefab {prefabId} → Entity {prefabEntity.Index}");

        var types = em.GetComponentTypes(prefabEntity, Allocator.Temp);
        try
        {
            ctx.Reply($"组件数量: {types.Length}");
            foreach (var comp in types)
            {
                string compName = comp.ToString();
                ctx.Reply($" - {compName}");
                Plugin.LogInstance.LogInfo($"[PrefabInfo] {prefabId} {compName}");
            }
        }
        finally
        {
            types.Dispose();
        }
    }

    [Command("spawnadd", shortHand: "a", adminOnly: true, usage: ".t a <Id> [数量] [半径]", description: "在鼠标位置生成任意Prefab实体，可选数量。若提供半径则圆圈分布，否则直线分布")]
      public static void SpawnAnyEntitytwo (ChatCommandContext ctx, int prefabId, int count = 1, float radius = -1f)
    {
        try
        {
            // 获取服务端世界
            World serverWorld = null;
            foreach (var world in World.All)
            {
                if (world.Name == "Server")
                {
                    serverWorld = world;
                    break;
                }
            }
            if (serverWorld == null)
            {
                ctx.Reply("未找到服务端世界。");
                return;
            }
            var em = serverWorld.EntityManager;

            // 获取玩家位置
            Entity character = ctx.Event.SenderCharacterEntity;
            if (!em.Exists(character) || !em.HasComponent<EntityInput>(character))
            {
                ctx.Reply("玩家实体无效或缺少 EntityInput 组件。");
                return;
            }
            var entityInput = em.GetComponentData<EntityInput>(character);
            float3 pos = entityInput.AimPosition;

            // 检查 Prefab
            var prefabGuid = new PrefabGUID(prefabId);
            var prefabSystem = serverWorld.GetExistingSystemManaged<PrefabCollectionSystem>();
            if (!prefabSystem._PrefabGuidToEntityMap.TryGetValue(prefabGuid, out var prefabEntity))
            {
                ctx.Reply($"无效的 PrefabID: {prefabId}");
                return;
            }

            // 如果是直线模式，计算间距
            float spacing = 4f;
            if (radius <= 0 && em.HasComponent<CollisionRadius>(prefabEntity))
            {
                var col = em.GetComponentData<CollisionRadius>(prefabEntity);
                if (col.Radius > 0f)
                    spacing = math.max(4f, col.Radius * 2.5f);
            }

            // 循环生成
            count = math.max(1, count);
            for (int i = 0; i < count; i++)
            {
                Entity spawned = em.Instantiate(prefabEntity);
                float3 offset;

                if (radius > 0) // 圆圈模式
                {
                    float angle = (math.PI * 2f) * i / count;
                    offset = new float3(
                        math.cos(angle) * radius,
                        0f,
                        math.sin(angle) * radius
                    );

                    // 让实体朝向圆心（可选）
                    em.SetComponentData(spawned, new Rotation
                    {
                        Value = quaternion.LookRotationSafe(math.normalize(pos - (pos + offset)), math.up())
                    });
                }
                else // 直线模式
                {
                    offset = new float3(i * spacing, 0, 0);
                }

                em.SetComponentData(spawned, new Translation { Value = pos + offset });
            }

          


            // 成功提示
            string mode = radius > 0 ? $"圆圈 r={radius}" : $"直线 spacing={spacing}";
            ctx.Reply($"已生成 {count} 个实体:  [ID:{prefabId}] 于 {pos}, 模式={mode}");
            Plugin.LogInstance.LogInfo($"[SpawnAny] Spawned {count} x ({prefabId}) at {pos}, mode={mode}");
        }
        catch (Exception ex)
        {
            ctx.Reply("生成实体失败，请查看服务器日志。");
            Plugin.LogInstance.LogError($"[SpawnAny] Exception: {ex}");
        }
    }





 
    








}