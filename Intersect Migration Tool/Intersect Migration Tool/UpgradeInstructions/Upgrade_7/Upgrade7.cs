﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Intersect_Migration_Tool.UpgradeInstructions.Upgrade_7.Intersect_Convert_Lib;
using Mono.Data.Sqlite;
using Intersect_Migration_Tool.UpgradeInstructions.Upgrade_7.Intersect_Convert_Lib.GameObjects;
using Intersect_Migration_Tool.UpgradeInstructions.Upgrade_7.Intersect_Convert_Lib.GameObjects.Events;
using Intersect_Migration_Tool.UpgradeInstructions.Upgrade_7.Intersect_Convert_Lib.GameObjects.Maps;
using Intersect_Migration_Tool.UpgradeInstructions.Upgrade_7.Intersect_Convert_Lib.GameObjects.Switches_and_Variables;

namespace Intersect_Migration_Tool.UpgradeInstructions.Upgrade_7
{
    public class Upgrade7
    {
        private SqliteConnection _dbConnection;
        private Object _dbLock = new Object();

        //GameObject Table Constants
        private const string GAME_OBJECT_ID = "id";
        private const string GAME_OBJECT_DELETED = "deleted";
        private const string GAME_OBJECT_DATA = "data";

        //Bag Table Constants
        private const string BAGS_TABLE = "bags";
        private const string BAG_ID = "bag_id";
        private const string BAG_SLOT_COUNT = "slot_count";

        //Bag Items Table Constants
        private const string BAG_ITEMS_TABLE = "bag_items";
        private const string BAG_ITEM_CONTAINER_ID = "bag_id";
        private const string BAG_ITEM_SLOT = "slot";
        private const string BAG_ITEM_NUM = "itemnum";
        private const string BAG_ITEM_VAL = "itemval";
        private const string BAG_ITEM_STATS = "itemstats";
        private const string BAG_ITEM_BAG_ID = "item_bag_id";

        //Char Inventory Table Constants
        private const string CHAR_INV_TABLE = "char_inventory";
        private const string CHAR_INV_ITEM_BAG_ID = "item_bag_id";
        //Char Bank Table Constants
        private const string CHAR_BANK_TABLE = "char_bank";
        private const string CHAR_BANK_ITEM_BAG_ID = "item_bag_id";




        public Upgrade7(SqliteConnection connection)
        {
            _dbConnection = connection;
        }

        public void Upgrade()
        {
            //Gotta Load and Save EVERYTHING
            //We are loading non-unicode strings and replacing them with unicode ones
            ServerOptions.LoadOptions();
            LoadAllGameObjects();
            CreateBagsTable();
            CreateBagItemsTable();
            AddBagColumnToInventory();
            AddBagColumnToBank();
            UpdateServerConfig();
        }

        private void UpdateServerConfig()
        {
            //Remove the Paperdoll element.. going to replace it
            XmlDocument doc = new XmlDocument();
            doc.Load(Path.Combine("resources","config.xml"));
            XmlNodeList nodes = doc.SelectNodes("//Config/Paperdoll");
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                nodes[i].ParentNode.RemoveChild(nodes[i]);
            }

            XmlNode rootElement = doc.SelectNodes("//Config")[0];

            //Create new direction based paperdolls
            XmlElement eleNew = doc.CreateElement("Paperdoll");

            var dirs = new string[] {"Up", "Down", "Left", "Right"};
            foreach (var dir in dirs)
            {
                XmlElement dirElement = doc.CreateElement(dir);
                XmlComment comment =
                    doc.CreateComment("Paperdoll is rendered in the following order when facing " + dir + ". If you want to change when each piece of equipment gets rendered simply swap the equipment names.");
                dirElement.AppendChild(comment);
                for (int i = 0; i < Options.PaperdollOrder.Count; i++)
                {
                    XmlElement slot = doc.CreateElement("Slot" + i);
                    slot.InnerText = Options.PaperdollOrder[i];
                    dirElement.AppendChild(slot);
                }
                eleNew.AppendChild(dirElement);
            }

            rootElement.AppendChild(eleNew);

            //Finally create the passability options
            eleNew = doc.CreateElement("Passability");
            var passabilityComment = doc.CreateComment("Trigger player passability based on map moralites. True = Passable, False = Blocked");
            XmlElement normal = doc.CreateElement("Normal");
            normal.InnerText = "False";
            XmlElement safe = doc.CreateElement("Safe");
            safe.InnerText = "True";
            XmlElement arena = doc.CreateElement("Arena");
            arena.InnerText = "False";
            eleNew.AppendChild(passabilityComment);
            eleNew.AppendChild(normal);
            eleNew.AppendChild(safe);
            eleNew.AppendChild(arena);

            rootElement.AppendChild(eleNew);

            doc.Save(Path.Combine("resources", "config.xml"));
        }

        //Game Object Saving/Loading
        private void LoadAllGameObjects()
        {

            foreach (var val in Enum.GetValues(typeof(GameObject)))
            {
                if ((GameObject)val != GameObject.Time)
                {
                    LoadGameObjects((GameObject)val);
                }
            }

        }
        //Game Object Saving/Loading
        private string GetGameObjectTable(GameObject type)
        {
            var tableName = "";
            switch (type)
            {
                case GameObject.Animation:
                    tableName = AnimationBase.DatabaseTable;
                    break;
                case GameObject.Bench:
                    tableName = BenchBase.DatabaseTable;
                    break;
                case GameObject.Class:
                    tableName = ClassBase.DatabaseTable;
                    break;
                case GameObject.Item:
                    tableName = ItemBase.DatabaseTable;
                    break;
                case GameObject.Npc:
                    tableName = NpcBase.DatabaseTable;
                    break;
                case GameObject.Projectile:
                    tableName = ProjectileBase.DatabaseTable;
                    break;
                case GameObject.Quest:
                    tableName = QuestBase.DatabaseTable;
                    break;
                case GameObject.Resource:
                    tableName = ResourceBase.DatabaseTable;
                    break;
                case GameObject.Shop:
                    tableName = ShopBase.DatabaseTable;
                    break;
                case GameObject.Spell:
                    tableName = SpellBase.DatabaseTable;
                    break;
                case GameObject.Map:
                    tableName = MapBase.DatabaseTable;
                    break;
                case GameObject.CommonEvent:
                    tableName = EventBase.DatabaseTable;
                    break;
                case GameObject.PlayerSwitch:
                    tableName = PlayerSwitchBase.DatabaseTable;
                    break;
                case GameObject.PlayerVariable:
                    tableName = PlayerVariableBase.DatabaseTable;
                    break;
                case GameObject.ServerSwitch:
                    tableName = ServerSwitchBase.DatabaseTable;
                    break;
                case GameObject.ServerVariable:
                    tableName = ServerVariableBase.DatabaseTable;
                    break;
                case GameObject.Tileset:
                    tableName = TilesetBase.DatabaseTable;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
            return tableName;
        }
        private void ClearGameObjects(GameObject type)
        {
            switch (type)
            {
                case GameObject.Animation:
                    AnimationBase.ClearObjects();
                    break;
                case GameObject.Bench:
                    BenchBase.ClearObjects();
                    break;
                case GameObject.Class:
                    ClassBase.ClearObjects();
                    break;
                case GameObject.Item:
                    ItemBase.ClearObjects();
                    break;
                case GameObject.Npc:
                    NpcBase.ClearObjects();
                    break;
                case GameObject.Projectile:
                    ProjectileBase.ClearObjects();
                    break;
                case GameObject.Quest:
                    QuestBase.ClearObjects();
                    break;
                case GameObject.Resource:
                    ResourceBase.ClearObjects();
                    break;
                case GameObject.Shop:
                    ShopBase.ClearObjects();
                    break;
                case GameObject.Spell:
                    SpellBase.ClearObjects();
                    break;
                case GameObject.Map:
                    MapBase.ClearObjects();
                    break;
                case GameObject.CommonEvent:
                    EventBase.ClearObjects();
                    break;
                case GameObject.PlayerSwitch:
                    PlayerSwitchBase.ClearObjects();
                    break;
                case GameObject.PlayerVariable:
                    PlayerVariableBase.ClearObjects();
                    break;
                case GameObject.ServerSwitch:
                    ServerSwitchBase.ClearObjects();
                    break;
                case GameObject.ServerVariable:
                    ServerVariableBase.ClearObjects();
                    break;
                case GameObject.Tileset:
                    TilesetBase.ClearObjects();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
        private void LoadGameObject(GameObject type, int index, byte[] data)
        {
            switch (type)
            {
                case GameObject.Animation:
                    var anim = new AnimationBase(index);
                    anim.Load(data);
                    AnimationBase.AddObject(index, anim);
                    SaveGameObject(anim);
                    break;
                case GameObject.Bench:
                    var bench = new BenchBase(index);
                    bench.Load(data);
                    BenchBase.AddObject(index, bench);
                    SaveGameObject(bench);
                    break;
                case GameObject.Class:
                    var cls = new ClassBase(index);
                    cls.Load(data);
                    ClassBase.AddObject(index, cls);
                    SaveGameObject(cls);
                    break;
                case GameObject.Item:
                    var itm = new ItemBase(index);
                    itm.Load(data);
                    ItemBase.AddObject(index, itm);
                    SaveGameObject(itm);
                    break;
                case GameObject.Npc:
                    var npc = new NpcBase(index);
                    npc.Load(data);
                    NpcBase.AddObject(index, npc);
                    SaveGameObject(npc);
                    break;
                case GameObject.Projectile:
                    var proj = new ProjectileBase(index);
                    proj.Load(data);
                    ProjectileBase.AddObject(index, proj);
                    SaveGameObject(proj);
                    break;
                case GameObject.Quest:
                    var qst = new QuestBase(index);
                    qst.Load(data);
                    QuestBase.AddObject(index, qst);
                    SaveGameObject(qst);
                    break;
                case GameObject.Resource:
                    var res = new ResourceBase(index);
                    res.Load(data);
                    ResourceBase.AddObject(index, res);
                    SaveGameObject(res);
                    break;
                case GameObject.Shop:
                    var shp = new ShopBase(index);
                    shp.Load(data);
                    ShopBase.AddObject(index, shp);
                    SaveGameObject(shp);
                    break;
                case GameObject.Spell:
                    var spl = new SpellBase(index);
                    spl.Load(data);
                    SpellBase.AddObject(index, spl);
                    SaveGameObject(spl);
                    break;
                case GameObject.Map:
                    var map = new MapBase(index, false);
                    MapBase.AddObject(index, map);
                    map.Load(data);
                    SaveGameObject(map);
                    break;
                case GameObject.CommonEvent:
                    var buffer = new ByteBuffer();
                    buffer.WriteBytes(data);
                    var evt = new EventBase(index, buffer, true);
                    EventBase.AddObject(index, evt);
                    buffer.Dispose();
                    SaveGameObject(evt);
                    break;
                case GameObject.PlayerSwitch:
                    var pswitch = new PlayerSwitchBase(index);
                    pswitch.Load(data);
                    PlayerSwitchBase.AddObject(index, pswitch);
                    SaveGameObject(pswitch);
                    break;
                case GameObject.PlayerVariable:
                    var pvar = new PlayerVariableBase(index);
                    pvar.Load(data);
                    PlayerVariableBase.AddObject(index, pvar);
                    SaveGameObject(pvar);
                    break;
                case GameObject.ServerSwitch:
                    var sswitch = new ServerSwitchBase(index);
                    sswitch.Load(data);
                    ServerSwitchBase.AddObject(index, sswitch);
                    SaveGameObject(sswitch);
                    break;
                case GameObject.ServerVariable:
                    var svar = new ServerVariableBase(index);
                    svar.Load(data);
                    ServerVariableBase.AddObject(index, svar);
                    SaveGameObject(svar);
                    break;
                case GameObject.Tileset:
                    var tset = new TilesetBase(index);
                    tset.Load(data);
                    TilesetBase.AddObject(index, tset);
                    SaveGameObject(tset);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
        public void LoadGameObjects(GameObject type)
        {
            var nullIssues = "";
            lock (_dbLock)
            {
                var tableName = GetGameObjectTable(type);
                ClearGameObjects(type);
                var query = "SELECT * from " + tableName + " WHERE " + GAME_OBJECT_DELETED + "=@" + GAME_OBJECT_DELETED +
                            ";";
                using (SqliteCommand cmd = new SqliteCommand(query, _dbConnection))
                {
                    cmd.Parameters.Add(new SqliteParameter("@" + GAME_OBJECT_DELETED, 0.ToString()));
                    using (var dataReader = cmd.ExecuteReader())
                    {
                        while (dataReader.Read())
                        {
                            var index = Convert.ToInt32(dataReader[GAME_OBJECT_ID]);
                            if (dataReader[GAME_OBJECT_DATA].GetType() != typeof(System.DBNull))
                            {
                                LoadGameObject(type, index, (byte[]) dataReader[GAME_OBJECT_DATA]);
                            }
                            else
                            {
                                nullIssues += "Tried to load null value for index " + index + " of " + tableName +
                                              Environment.NewLine;
                            }
                        }
                    }
                }
            }
            if (nullIssues != "")
            {
                throw (new Exception("Tried to load one or more null game objects!" + Environment.NewLine + nullIssues));
            }
        }
        public void SaveGameObject(DatabaseObject gameObject)
        {
            lock (_dbLock)
            {
                var insertQuery = "UPDATE " + gameObject.GetTable() + " set " + GAME_OBJECT_DELETED + "=@" +
                                  GAME_OBJECT_DELETED + "," + GAME_OBJECT_DATA + "=@" + GAME_OBJECT_DATA + " WHERE " +
                                  GAME_OBJECT_ID + "=@" + GAME_OBJECT_ID + ";";
                using (SqliteCommand cmd = new SqliteCommand(insertQuery, _dbConnection))
                {
                    cmd.Parameters.Add(new SqliteParameter("@" + GAME_OBJECT_ID, gameObject.GetId()));
                    cmd.Parameters.Add(new SqliteParameter("@" + GAME_OBJECT_DELETED, 0.ToString()));
                    if (gameObject != null && gameObject.GetData() != null)
                    {
                        cmd.Parameters.Add(new SqliteParameter("@" + GAME_OBJECT_DATA, gameObject.GetData()));
                    }
                    else
                    {
                        throw (new Exception("Tried to save a null game object (should be deleted instead?) Table: " +
                                             gameObject.GetTable() + " Id: " + gameObject.GetId()));
                    }
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void CreateBagsTable()
        {
            var cmd = "CREATE TABLE " + BAGS_TABLE + " ("
                + BAG_ID + " INTEGER PRIMARY KEY AUTOINCREMENT,"
                + BAG_SLOT_COUNT + " INTEGER"
                + ");";
            using (var createCommand = _dbConnection.CreateCommand())
            {
                createCommand.CommandText = cmd;
                createCommand.ExecuteNonQuery();
            }
        }
        private void CreateBagItemsTable()
        {
            var cmd = "CREATE TABLE " + BAG_ITEMS_TABLE + " ("
                + BAG_ITEM_CONTAINER_ID + " INTEGER,"
                + BAG_ITEM_SLOT + " INTEGER,"
                + BAG_ITEM_NUM + " INTEGER,"
                + BAG_ITEM_VAL + " INTEGER,"
                + BAG_ITEM_STATS + " TEXT,"
                + BAG_ITEM_BAG_ID + " TEXT,"
                + " unique('" + BAG_ITEM_CONTAINER_ID + "','" + BAG_ITEM_SLOT + "')"
                + ");";
            using (var createCommand = _dbConnection.CreateCommand())
            {
                createCommand.CommandText = cmd;
                createCommand.ExecuteNonQuery();
            }
            CreateBag(1); //This is to bypass an issue where we use itemVal to store the bag identifier (we are terrible!)
        }

        public void CreateBag(int slotCount)
        {
            var insertQuery = "INSERT into " + BAGS_TABLE + " (" + BAG_SLOT_COUNT + ")" + "VALUES (@" + BAG_SLOT_COUNT + ");";
            using (SqliteCommand cmd = new SqliteCommand(insertQuery, _dbConnection))
            {
                cmd.Parameters.Add(new SqliteParameter("@" + BAG_SLOT_COUNT, slotCount));
                cmd.ExecuteNonQuery();
            }
        }

        private void AddBagColumnToBank()
        {
            var cmd = "ALTER TABLE " + CHAR_BANK_TABLE + " ADD " + CHAR_BANK_ITEM_BAG_ID + " INTEGER;";
            using (var createCommand = _dbConnection.CreateCommand())
            {
                createCommand.CommandText = cmd;
                createCommand.ExecuteNonQuery();
            }
        }

        private void AddBagColumnToInventory()
        {
            var cmd = "ALTER TABLE " + CHAR_INV_TABLE + " ADD " + CHAR_INV_ITEM_BAG_ID + " INTEGER;";
            using (var createCommand = _dbConnection.CreateCommand())
            {
                createCommand.CommandText = cmd;
                createCommand.ExecuteNonQuery();
            }
        }

    }
}