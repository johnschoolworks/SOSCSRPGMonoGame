using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using SoscMonoGame.Engine;

namespace SoscMonoGame;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private Desktop? _desktop;
    private GameSession _gameSession = null!;

    private Label _lblPlayerHp = null!;
    private Label _lblPlayerGold = null!;
    private Label _lblMonsterInfo = null!;
    private Label _lblLocation = null!;
    private Label _lblWeapon = null!;
    private Label _lblQuest = null!;
    private VerticalStackPanel _inventoryContainer = null!;
    private VerticalStackPanel _logContainer = null!;
    private VerticalStackPanel _traderContainer = null!;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        IsMouseVisible = true;
        _graphics.PreferredBackBufferWidth = 850;
        _graphics.PreferredBackBufferHeight = 980;
    }

    protected override void Initialize()
    {
        _gameSession = new GameSession();
        _gameSession.StateChanged += OnSessionChanged;
        MyraEnvironment.Game = this;
        base.Initialize();
    }

    private void OnSessionChanged(object? sender, System.EventArgs args)
    {
        if (_desktop is not null) RefreshView();
    }

    protected override void LoadContent()
    {
        var mainGrid = new Grid
        {
            RowSpacing = 10,
            ColumnSpacing = 10,
            Padding = new Myra.Graphics2D.Thickness(15)
        };
        mainGrid.ColumnsProportions.Add(new Proportion(ProportionType.Part, 1));
        mainGrid.ColumnsProportions.Add(new Proportion(ProportionType.Part, 1));
        mainGrid.RowsProportions.Add(new Proportion(ProportionType.Auto));
        mainGrid.RowsProportions.Add(new Proportion(ProportionType.Auto));
        mainGrid.RowsProportions.Add(new Proportion(ProportionType.Auto));
        mainGrid.RowsProportions.Add(new Proportion(ProportionType.Auto));
        mainGrid.RowsProportions.Add(new Proportion(ProportionType.Auto));
        mainGrid.RowsProportions.Add(new Proportion(ProportionType.Auto));

        mainGrid.RowsProportions.Add(new Proportion(ProportionType.Auto));
        mainGrid.RowsProportions.Add(new Proportion(ProportionType.Auto));

        // 1. Left Card: Player Stats
        var playerCard = new Panel
        {
            Background = new SolidBrush(new Color(30, 30, 40)),
            Padding = new Myra.Graphics2D.Thickness(10)
        };
        var pStack = new VerticalStackPanel { Spacing = 6 };
        pStack.Widgets.Add(new Label { Text = $"Player: {_gameSession.CurrentPlayer.Name}", TextColor = Color.Gold });
        _lblPlayerHp = new Label { TextColor = Color.LightGreen };
        _lblPlayerGold = new Label { TextColor = Color.Yellow };
        pStack.Widgets.Add(_lblPlayerHp);
        pStack.Widgets.Add(_lblPlayerGold);
        _lblWeapon = new Label { TextColor = Color.LightBlue };
        pStack.Widgets.Add(_lblWeapon);
        playerCard.Widgets.Add(pStack);
        Grid.SetColumn(playerCard, 0);
        Grid.SetRow(playerCard, 0);
        mainGrid.Widgets.Add(playerCard);

        // 2. Right Card: Current Monster
        var monsterCard = new Panel
        {
            Background = new SolidBrush(new Color(40, 25, 25)),
            Padding = new Myra.Graphics2D.Thickness(10)
        };
        var mStack = new VerticalStackPanel { Spacing = 6 };
        mStack.Widgets.Add(new Label { Text = "=== Combat Encounter ===", TextColor = Color.IndianRed });
        _lblMonsterInfo = new Label { TextColor = Color.OrangeRed };
        mStack.Widgets.Add(_lblMonsterInfo);
        monsterCard.Widgets.Add(mStack);
        Grid.SetColumn(monsterCard, 1);
        Grid.SetRow(monsterCard, 0);
        mainGrid.Widgets.Add(monsterCard);

        // 3. Current location and directions
        var locationPanel = new Panel
        {
            Background = new SolidBrush(new Color(24, 35, 40)),
            Padding = new Myra.Graphics2D.Thickness(10)
        };
        var locationStack = new VerticalStackPanel { Spacing = 6 };
        _lblLocation = new Label { TextColor = Color.LightBlue };
        locationStack.Widgets.Add(_lblLocation);
        var directions = new HorizontalStackPanel { Spacing = 8 };
        AddMoveButton(directions, "North", Direction.North);
        AddMoveButton(directions, "South", Direction.South);
        AddMoveButton(directions, "East", Direction.East);
        AddMoveButton(directions, "West", Direction.West);
        locationStack.Widgets.Add(directions);
        locationPanel.Widgets.Add(locationStack);
        Grid.SetColumn(locationPanel, 0);
        Grid.SetColumnSpan(locationPanel, 2);
        Grid.SetRow(locationPanel, 1);
        mainGrid.Widgets.Add(locationPanel);

        // 4. Quest board status
        var questPanel = new Panel
        {
            Background = new SolidBrush(new Color(34, 35, 48)),
            Padding = new Myra.Graphics2D.Thickness(10)
        };
        var questStack = new VerticalStackPanel { Spacing = 5 };
        _lblQuest = new Label { TextColor = Color.LightGoldenrodYellow };
        questStack.Widgets.Add(_lblQuest);
        var btnTurnIn = new Button { Content = new Label { Text = "Turn In Quest (Town Square)" } };
        btnTurnIn.Click += (s, a) => { _gameSession.TurnInQuest(); };
        questStack.Widgets.Add(btnTurnIn);
        questPanel.Widgets.Add(questStack);
        Grid.SetColumn(questPanel, 0);
        Grid.SetColumnSpan(questPanel, 2);
        Grid.SetRow(questPanel, 2);
        mainGrid.Widgets.Add(questPanel);

        // 5. Center Panel: Rolling Combat Log
        var logPanel = new Panel
        {
            Background = new SolidBrush(new Color(18, 18, 22)),
            Padding = new Myra.Graphics2D.Thickness(10)
        };
        _logContainer = new VerticalStackPanel { Spacing = 4 };
        logPanel.Widgets.Add(_logContainer);
        Grid.SetColumn(logPanel, 0);
        Grid.SetColumnSpan(logPanel, 2);
        Grid.SetRow(logPanel, 3);
        mainGrid.Widgets.Add(logPanel);

        // 5. Inventory: actual item counts, not a decorative list
        var inventoryPanel = new Panel
        {
            Background = new SolidBrush(new Color(28, 32, 38)),
            Padding = new Myra.Graphics2D.Thickness(10)
        };
        _inventoryContainer = new VerticalStackPanel { Spacing = 3 };
        inventoryPanel.Widgets.Add(_inventoryContainer);
        Grid.SetColumn(inventoryPanel, 0);
        Grid.SetColumnSpan(inventoryPanel, 2);
        Grid.SetRow(inventoryPanel, 4);
        mainGrid.Widgets.Add(inventoryPanel);

        // 6. Town Square merchant (buy/sell buttons are generated from live inventories)
        var traderPanel = new Panel
        {
            Background = new SolidBrush(new Color(32, 38, 33)),
            Padding = new Myra.Graphics2D.Thickness(10)
        };
        _traderContainer = new VerticalStackPanel { Spacing = 4 };
        traderPanel.Widgets.Add(_traderContainer);
        Grid.SetColumn(traderPanel, 0);
        Grid.SetColumnSpan(traderPanel, 2);
        Grid.SetRow(traderPanel, 5);
        mainGrid.Widgets.Add(traderPanel);

        // 7. Bottom Row: Action Buttons
        var buttonBar = new HorizontalStackPanel { Spacing = 12 };
        var btnAttack = new Button { Content = new Label { Text = "Attack with Sword" } };
        btnAttack.Click += (s, a) =>
        {
            _gameSession.Attack();
        };

        var btnPotion = new Button { Content = new Label { Text = "Drink Potion" } };
        btnPotion.Click += (s, a) =>
        {
            _gameSession.UsePotion();
        };

        buttonBar.Widgets.Add(btnAttack);
        buttonBar.Widgets.Add(btnPotion);
        var btnRusty = new Button { Content = new Label { Text = "Equip Rusty" } };
        btnRusty.Click += (s, a) => { _gameSession.EquipRustySword(); };
        buttonBar.Widgets.Add(btnRusty);
        var btnIron = new Button { Content = new Label { Text = "Equip Iron" } };
        btnIron.Click += (s, a) => { _gameSession.EquipIronSword(); };
        buttonBar.Widgets.Add(btnIron);
        Grid.SetColumn(buttonBar, 0);
        Grid.SetColumnSpan(buttonBar, 2);
        Grid.SetRow(buttonBar, 6);
        mainGrid.Widgets.Add(buttonBar);

        var saveBar = new HorizontalStackPanel { Spacing = 12 };
        var saveButton = new Button { Content = new Label { Text = "Save Game" } };
        saveButton.Click += (sender, args) =>
        {
            try { GameSaveService.Save(_gameSession); _gameSession.AddSystemMessage("Saved: " + GameSaveService.DefaultPath); }
            catch (System.Exception ex) { _gameSession.AddSystemMessage("Save failed: " + ex.Message); }
        };
        var loadButton = new Button { Content = new Label { Text = "Load Game" } };
        loadButton.Click += (sender, args) =>
        {
            try
            {
                var loaded = GameSaveService.Load();
                _gameSession.StateChanged -= OnSessionChanged;
                _gameSession = loaded;
                _gameSession.StateChanged += OnSessionChanged;
                RefreshView();
            }
            catch (System.Exception ex) { _gameSession.AddSystemMessage("Load failed: " + ex.Message); }
        };
        saveBar.Widgets.Add(saveButton);
        saveBar.Widgets.Add(loadButton);
        Grid.SetColumn(saveBar, 0);
        Grid.SetColumnSpan(saveBar, 2);
        Grid.SetRow(saveBar, 7);
        mainGrid.Widgets.Add(saveBar);

        _desktop = new Desktop { Root = mainGrid };
        RefreshView();
    }

    private void AddMoveButton(HorizontalStackPanel panel, string label, Direction direction)
    {
        var button = new Button { Content = new Label { Text = label } };
        button.Click += (s, a) =>
        {
            _gameSession.Move(direction);
        };
        panel.Widgets.Add(button);
    }

    private void RefreshView()
    {
        var p = _gameSession.CurrentPlayer;
        _lblPlayerHp.Text = $"HP: {p.CurrentHitPoints} / {p.MaximumHitPoints}";
        _lblPlayerGold.Text = $"Gold: {p.Gold} G | Level: {p.Level} | XP: {p.ExperiencePoints}/{p.ExperienceToNextLevel}";
        _lblWeapon.Text = $"Equipped: {p.EquippedWeapon.Name} ({p.EquippedWeapon.MinimumDamage}-{p.EquippedWeapon.MaximumDamage} dmg)";
        var quest = _gameSession.ActiveQuest;
        _lblQuest.Text = quest is null ? "All quests completed!" :
            $"Quest: {quest.Quest.Name} — {ItemFactory.Get(quest.Quest.RequiredItemId).Name} " +
            $"{System.Math.Min(quest.Quest.RequiredQuantity, p.Inventory.Count(quest.Quest.RequiredItemId))}/{quest.Quest.RequiredQuantity}";
        _traderContainer.Widgets.Clear();
        _traderContainer.Widgets.Add(new Label
        {
            Text = _gameSession.IsAtTrader ? "=== Mara the Merchant (Town Square) ===" : "Merchant: visit Town Square to trade",
            TextColor = Color.Gold
        });
        if (_gameSession.IsAtTrader)
        {
            foreach (var entry in _gameSession.TownMerchant.Stock.Entries)
            {
                int itemId = entry.Item.Id;
                var buy = new Button { Content = new Label { Text = $"Buy {entry.Item.Name} ({_gameSession.TownMerchant.BuyPrice(entry.Item)} G) — stock {entry.Quantity}" } };
                buy.Click += (sender, args) => { _gameSession.BuyItem(itemId); };
                _traderContainer.Widgets.Add(buy);
            }
            foreach (var entry in p.Inventory.Entries)
            {
                int itemId = entry.Item.Id;
                var sell = new Button { Content = new Label { Text = $"Sell {entry.Item.Name} ({_gameSession.TownMerchant.SellPrice(entry.Item)} G) — owned {entry.Quantity}" } };
                sell.Click += (sender, args) => { _gameSession.SellItem(itemId); };
                _traderContainer.Widgets.Add(sell);
            }
        }
        _inventoryContainer.Widgets.Clear();
        _inventoryContainer.Widgets.Add(new Label { Text = "=== Inventory ===", TextColor = Color.Gold });
        foreach (var entry in p.Inventory.Entries)
        {
            _inventoryContainer.Widgets.Add(new Label
            {
                Text = $"{entry.Item.Name} x{entry.Quantity}",
                TextColor = Color.LightGray
            });
        }

        var location = _gameSession.CurrentLocation;
        _lblLocation.Text = $"Location: {location.Name} — {location.Description}";

        var m = _gameSession.CurrentMonster;
        _lblMonsterInfo.Text = m != null
            ? $"Target: {m.Name}\nHP: {m.CurrentHitPoints} / {m.MaximumHitPoints}"
            : "No enemies nearby.";

        _logContainer.Widgets.Clear();
        foreach (var msg in _gameSession.CombatLog)
        {
            _logContainer.Widgets.Add(new Label { Text = msg, TextColor = Color.LightGray });
        }
    }

    protected override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(15, 15, 18));
        _desktop?.Render();
        base.Draw(gameTime);
    }
}
